#!/usr/bin/env bash
# PreToolUse guard for FishGame (Bash, Edit, Write, MultiEdit, NotebookEdit).
# Exit 2 blocks the tool call and shows stderr to Claude. Needs only bash, sed, awk, tr and git,
# so it runs in Git Bash on the Windows PC and on the Linux cloud VM (no jq, python or node).
set -f
input=$(cat)

json_field() {
    # JSON string value of key "$1" (unquoted keys only, so text inside other values can't match),
    # unescaped: \\ \" \n \t.
    printf '%s' "$input" | sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\(\([^\"\\\\]\|\\\\.\)*\)\".*/\1/p" \
        | head -n 1 \
        | sed -e 's/\\\\/\x01/g' -e 's/\\n/\n/g' -e 's/\\t/ /g' -e 's/\\"/"/g' -e 's/\x01/\\/g'
}

block() {
    echo "Blocked by .claude/hooks/guard.sh: $1" >&2
    exit 2
}

tool=$(json_field tool_name)
cwd=$(json_field cwd)
[ -n "$cwd" ] && cd "$cwd" 2>/dev/null

case "$tool" in
    Edit|Write|MultiEdit|NotebookEdit)
        path=$(json_field file_path)
        [ -z "$path" ] && path=$(json_field notebook_path)
        case "$path" in
            *.unity|*.prefab)
                block "don't edit Unity scene/prefab YAML by hand ($path). Every scene and prefab comes from a builder in Assets/_Project/Editor/Builders; change the builder and rebuild with Tools/unity.ps1 build." ;;
        esac
        exit 0 ;;
    Bash) ;;
    *) exit 0 ;;
esac

cmd=$(json_field command)
[ -z "$cmd" ] && exit 0

# Replace quoted strings with Q and drop heredoc bodies, so commit messages and file contents
# are never read as commands. Then split into one simple command per line.
segments=$(printf '%s\n' "$cmd" | awk '
    { s = s $0 "\n" }
    END {
        n = length(s); i = 1; out = ""; hd = ""; pend = ""
        while (i <= n) {
            c = substr(s, i, 1)
            if (hd != "") {
                j = index(substr(s, i), "\n"); if (j == 0) break
                line = substr(s, i, j - 1); sub(/^[ \t]+/, "", line); i += j
                if (line == hd) { hd = ""; out = out "\n" }
                continue
            }
            if (c == "\\") { out = out " "; i += 2; continue }
            if (c == "\047") { j = index(substr(s, i + 1), "\047"); i = (j == 0) ? n + 1 : i + j + 1; out = out " Q "; continue }
            if (c == "\"") {
                k = i + 1
                while (k <= n) { d = substr(s, k, 1); if (d == "\\") { k += 2; continue } if (d == "\"") break; k++ }
                i = k + 1; out = out " Q "; continue
            }
            if (substr(s, i, 2) == "<<") {
                rest = substr(s, i + 2)
                if (match(rest, /^-?[ \t]*["\047]?[A-Za-z_][A-Za-z0-9_]*["\047]?/)) {
                    w = substr(rest, RSTART, RLENGTH); gsub(/[-"\047 \t]/, "", w)
                    pend = w; i += 2 + RLENGTH; continue
                }
            }
            if (c == "\n" && pend != "") { hd = pend; pend = "" }
            out = out c; i++
        }
        print out
    }' | tr ';&|(){}' '\n\n\n\n\n\n\n' \
       | sed -E -e 's/^[[:space:]]+//' -e 's/[[:space:]]+$//' -e 's/^git[[:space:]]+-C[[:space:]]+[^[:space:]]+/git/')

branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
common=$(git rev-parse --git-common-dir 2>/dev/null)

while IFS= read -r seg; do
    [ -z "$seg" ] && continue

    # In-place rewrites of scene/prefab YAML.
    if printf '%s' "$seg" | grep -Eq '^(sed|perl)([[:space:]].*)?[[:space:]]-i' && printf '%s' "$seg" | grep -Eq '\.(unity|prefab)([[:space:]]|$)'; then
        block "don't rewrite .unity/.prefab files with sed/perl; change the generating builder instead."
    fi

    # Launching Unity directly instead of through the wrapper. Process lookups put -projectPath in quotes,
    # which are stripped above, so they don't match.
    if printf '%s' "$seg" | grep -Eq '(^|[[:space:]])-projectPath([[:space:]]|$)' \
       && printf '%s' "$cmd" | grep -Eiq 'unity' && ! printf '%s' "$seg" | grep -Eiq 'unity\.ps1'; then
        block "run Unity through Tools/unity.ps1 (it checks the lock, adds -dev-profile fgtest, reads the log and records the PASS marker)."
    fi

    read -r -a words <<< "$seg"
    [ "${words[0]}" = "git" ] || continue
    sub="${words[1]}"
    args=("${words[@]:2}")

    case "$sub" in
        checkout|switch)
            # Track a branch change earlier in the same command line.
            last="${args[${#args[@]}-1]}"
            case " ${args[*]} " in *" -b "*|*" -c "*|*" -- "*) ;; *) [ -n "$last" ] && [ "${last#-}" = "$last" ] && branch="$last" ;; esac
            ;;
        push)
            refs=0
            for word in "${args[@]}"; do
                case "$word" in
                    --force|--force=*|--force-with-lease*|--force-if-includes|+*) block "never force-push (CLAUDE.md)." ;;
                    --*) ;;
                    -*f*) block "never force-push (CLAUDE.md)." ;;
                esac
                case "$word" in -*) ;; *) refs=$((refs + 1)) ;; esac
                if [ "$CLAUDE_CODE_REMOTE" = "true" ]; then
                    case "$word" in
                        main|*:main|*:refs/heads/main|refs/heads/main)
                            block "cloud sessions never push to main; push a feature branch and let the local controller merge (docs/superpowers/handoff/cloud-environment.md)." ;;
                    esac
                fi
            done
            if [ "$CLAUDE_CODE_REMOTE" = "true" ] && [ "$branch" = "main" ] && [ "$refs" -le 1 ]; then
                block "cloud sessions never push to main; push a feature branch and let the local controller merge."
            fi
            ;;
        merge)
            # Merge gate: merging into main needs a passing Tools/unity.ps1 check on each merged commit.
            [ "$branch" = "main" ] || continue
            skip=0
            for word in "${args[@]}"; do
                if [ "$skip" = 1 ]; then skip=0; continue; fi
                case "$word" in
                    --abort|--continue|--quit) continue 2 ;;
                    -m|--message|-F|--file|-s|--strategy|-X|--strategy-option) skip=1; continue ;;
                    -*|Q|main|HEAD|origin/main|FETCH_HEAD) continue ;;
                esac
                sha=$(git rev-parse --verify --quiet "$word^{commit}" 2>/dev/null) || continue
                if [ ! -f "$common/fg-check-pass/$sha" ]; then
                    block "no passing 'Tools/unity.ps1 check' recorded for $word ($sha). Check out that commit in C:\\Dev\\FishGame (not a worktree), run check (it writes $common/fg-check-pass/$sha), then merge. If the user explicitly waives the check for this merge, create that marker file by hand and tell them."
                fi
            done
            ;;
    esac
done <<< "$segments"

exit 0

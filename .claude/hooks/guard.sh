#!/usr/bin/env bash
# PreToolUse guard for FishGame (Bash, Edit, Write, MultiEdit, NotebookEdit).
# Exit 2 blocks the tool call and shows stderr to Claude. Pure bash + sed + git:
# runs in Git Bash on the Windows PC and on the Linux cloud VM (no jq, python or node).
input=$(cat)

json_field() {
    # Raw JSON string value of the first "$1" key, with \" and \\ unescaped.
    printf '%s' "$input" | sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\(\([^\"\\\\]\|\\\\.\)*\)\".*/\1/p" \
        | head -n 1 | sed -e 's/\\"/"/g' -e 's/\\\\/\\/g'
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
                block "don't edit Unity scene/prefab YAML by hand ($path). Change the editor script in FishGame.EditorTools that generates it and rebuild with Tools/unity.ps1 build." ;;
        esac
        exit 0 ;;
    Bash) ;;
    *) exit 0 ;;
esac

cmd=$(json_field command)
[ -z "$cmd" ] && exit 0

# In-place rewrites of scene/prefab YAML.
if printf '%s' "$cmd" | grep -Eq '(sed|perl)[^|;&]* -i' && printf '%s' "$cmd" | grep -Eq '\.(unity|prefab)\b'; then
    block "don't rewrite .unity/.prefab files with sed/perl; change the generating editor script instead."
fi

# Launching Unity directly instead of through the wrapper (process lookups are fine).
if printf '%s' "$cmd" | grep -Eiq 'Unity(\.exe)?["'"'"']?[^|;&]*-projectPath' \
   && ! printf '%s' "$cmd" | grep -Eiq 'unity\.ps1|Get-CimInstance|Get-Process|Get-WmiObject|tasklist|wmic'; then
    block "run Unity through Tools/unity.ps1 (it checks the lock, adds -dev-profile fgtest, reads the log and records the PASS marker)."
fi

push_args=$(printf '%s' "$cmd" | sed -E -n 's/.*(^|[^[:alnum:]_-])git([[:space:]]+-C[[:space:]]+[^[:space:]]+)?[[:space:]]+push([^;&|]*).*/\3/p')
if printf '%s' "$cmd" | grep -Eq '(^|[^[:alnum:]_-])git([[:space:]]+-C[[:space:]]+[^[:space:]]+)?[[:space:]]+push'; then
    if printf ' %s' "$push_args" | grep -Eq '[[:space:]](--force([=[:space:]]|-with-lease|$)|-f([[:space:]]|$)|\+[^[:space:]]+)'; then
        block "never force-push (CLAUDE.md)."
    fi
    if [ "$CLAUDE_CODE_REMOTE" = "true" ]; then
        branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
        # Words after "push" that aren't options: none or just the remote means "push the current branch".
        refs=0
        for word in $push_args; do
            case "$word" in -*) ;; *) refs=$((refs + 1)) ;; esac
        done
        if printf ' %s ' "$push_args" | grep -Eq '([[:space:]]|:)main[[:space:]]' \
           || { [ "$branch" = "main" ] && [ "$refs" -le 1 ]; }; then
            block "cloud sessions never push to main; push a feature branch and let the local controller merge (docs/superpowers/handoff/cloud-environment.md)."
        fi
    fi
fi

# Merge gate: merging into main needs a passing Tools/unity.ps1 check on each merged commit.
if printf '%s' "$cmd" | grep -Eq '(^|[^[:alnum:]_-])git[[:space:]]+merge[[:space:]]' \
   && ! printf '%s' "$cmd" | grep -Eq 'git[[:space:]]+merge[[:space:]]+--(abort|continue|quit)'; then
    branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
    if [ "$branch" = "main" ]; then
        common=$(git rev-parse --git-common-dir 2>/dev/null)
        # Drop -m/--message arguments, then take the words after "merge" up to the next shell operator.
        args=$(printf '%s' "$cmd" \
            | sed -E -e 's/(-m|--message)[[:space:]=]*"([^"\\]|\\.)*"//g' -e "s/(-m|--message)[[:space:]=]*'[^']*'//g" \
            | sed -E -n 's/.*git[[:space:]]+merge[[:space:]]+([^;&|]*).*/\1/p')
        for word in $args; do
            case "$word" in -*|main|HEAD) continue ;; esac
            sha=$(git rev-parse --verify --quiet "$word^{commit}" 2>/dev/null) || continue
            if [ ! -f "$common/fg-check-pass/$sha" ]; then
                block "no passing 'Tools/unity.ps1 check' recorded for $word ($sha). Run check with that commit checked out (it writes $common/fg-check-pass/$sha), then merge. If the user explicitly waives the check for this merge, create that marker file by hand and tell them."
            fi
        done
    fi
fi

exit 0

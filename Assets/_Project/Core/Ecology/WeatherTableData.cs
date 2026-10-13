// Core/Ecology/WeatherTableData.cs: the weather Markov table (spec 12.2). Authority: none (plain data; WeatherDirector validates it).
namespace FishGame.Core
{
    public struct WeatherTableData
    {
        public float[] Base;               // 9, row-major: rows current Clear/Rain/Fog, columns next Clear/Rain/Fog (spec 12.2)
        public float[] SeasonColumnMult;   // 12: season * 3 + column (Spring Rain 1.5, Summer Fog 0.3, Winter Fog 1.3, others 1)
    }
}

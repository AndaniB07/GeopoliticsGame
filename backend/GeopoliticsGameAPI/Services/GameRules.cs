
using System.Net.NetworkInformation;

namespace GeopoliticsGameAPI.Services
{
    public class GameRules
    {
        public static int CalculateStartingSoldiers( double populationMillions)
        {
            if (populationMillions < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(populationMillions), "Population cannot be negative.");
            }

            if (populationMillions <= 5)
                return 1;
            if (populationMillions <= 10)
                return 2;
            if (populationMillions <= 25)
                return 3;
            if (populationMillions <= 50)
                return 4;
            if ( populationMillions <= 75)
                return 5;
            if (populationMillions <= 100)
                return 6;
            if (populationMillions <= 125)
                return 7;
            if (populationMillions <= 150)
                return 8;

            return 9;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Common
{
    public static class UserUtils
    {
        public static int CalculateAge(DateOnly birthday)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - birthday.Year;
            if (today < birthday.AddYears(age)) age--;
            return age;
        }

        public static string GetZodiacSign(DateOnly birthday)
        {
            var day = birthday.Day;
            var month = birthday.Month;
            return (month, day) switch
            {
                (3, >= 21) or (4, <= 19) => "Овен",
                (4, >= 20) or (5, <= 20) => "Телец",
                (5, >= 21) or (6, <= 20) => "Близнецы",
                (6, >= 21) or (7, <= 22) => "Рак",
                (7, >= 23) or (8, <= 22) => "Лев",
                (8, >= 23) or (9, <= 22) => "Дева",
                (9, >= 23) or (10, <= 22) => "Весы",
                (10, >= 23) or (11, <= 21) => "Скорпион",
                (11, >= 22) or (12, <= 21) => "Стрелец",
                (12, >= 22) or (1, <= 19) => "Козерог",
                (1, >= 20) or (2, <= 18) => "Водолей",
                (2, >= 19) or (3, <= 20) => "Рыбы",
                _ => string.Empty
            };
        }
    }
}

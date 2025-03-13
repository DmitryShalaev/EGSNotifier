using System.Text;

namespace Core.Bot {
    public static class SpecialCharacters {
        /// <summary>
        /// Массив булевых значений, указывающий, какие символы нужно экранировать.
        /// </summary>
        private static readonly bool[] SpecialCharsMap = new bool[128];

        /// <summary>
        /// Статический конструктор, инициализирующий <see cref="SpecialCharsMap"/>.
        /// </summary>
        static SpecialCharacters() {
            SpecialCharsMap['_'] = true;
            SpecialCharsMap['-'] = true;
            SpecialCharsMap['['] = true;
            SpecialCharsMap['!'] = true;
            SpecialCharsMap['.'] = true;
        }

        /// <summary>
        /// Экранирует специальные символы в переданной строке, добавляя перед ними символ '\'.
        /// </summary>
        /// <param name="input">Входная строка, в которой нужно экранировать символы.</param>
        /// <returns>Строка с экранированными символами или исходная строка, если <paramref name="input"/> равна <c>null</c> или пуста.</returns>
        public static string Escape(string input) {
            if(string.IsNullOrEmpty(input))
                return input;

            var sb = new StringBuilder(input.Length);

            foreach(char c in input) {
                // Проверяем, укладывается ли символ в границы массива и является ли он спецсимволом
                if(c < 128 && SpecialCharsMap[c])
                    sb.Append('\\');

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}

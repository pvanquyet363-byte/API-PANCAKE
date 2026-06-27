using System.Text.Json;

namespace API_PanCake.Helpers;

public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var result = new char[name.Length + Math.Min(name.Length, 10)];
        var resultIndex = 0;

        for (int i = 0; i < name.Length; i++)
        {
            var currentChar = name[i];
            
            if (i > 0 && char.IsUpper(currentChar))
            {
                result[resultIndex++] = '_';
            }
            
            result[resultIndex++] = char.ToLowerInvariant(currentChar);
        }

        return new string(result, 0, resultIndex);
    }
}

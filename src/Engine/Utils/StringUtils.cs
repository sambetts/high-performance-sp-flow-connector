namespace Engine.Utils;

public static class StringUtils
{

    /// <summary>
    /// To replace "string.TrimStart" as this sometimes doesn't work
    /// </summary>
    public static string TrimStringFromStart(this string strInput, string toTrim)
    {
        if (string.IsNullOrWhiteSpace(strInput)) throw new ArgumentNullException(nameof(strInput));
        if (string.IsNullOrEmpty(toTrim))
        {
            throw new ArgumentException($"'{nameof(toTrim)}' cannot be null or empty.", nameof(toTrim));
        }

        if (strInput.StartsWith(toTrim))
        {
            return strInput.Substring(toTrim.Length, strInput.Length - toTrim.Length);
        }
        else
            throw new ArgumentException($"'{strInput}' doesn't start with '{toTrim}'");
    }


    /// <summary>
    /// To replace "string.TrimEnd" as this sometimes doesn't work
    /// </summary>
    public static string TrimStringFromEnd(this string strInput, string toTrim)
    {
        if (string.IsNullOrWhiteSpace(strInput)) throw new ArgumentNullException(nameof(strInput));
        if (string.IsNullOrEmpty(toTrim))
        {
            throw new ArgumentException($"'{nameof(toTrim)}' cannot be null or empty.", nameof(toTrim));
        }

        if (strInput.EndsWith(toTrim))
        {
            return strInput.Substring(0, strInput.IndexOf(toTrim));
        }
        else
            throw new ArgumentException($"'{strInput}' doesn't end with '{toTrim}'");
    }
}

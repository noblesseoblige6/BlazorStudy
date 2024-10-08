﻿namespace TimelineSample.Shared
{
    public class StringValueAttribute : Attribute
    {
        public string StringValue { get; protected set; }

        public StringValueAttribute(string value)
        {
            this.StringValue = value;
        }
    }

    public static class CommonAttribute
    {

        public static string? GetStringValue(this Enum value)
        {
            Type type = value.GetType();

            System.Reflection.FieldInfo? fieldInfo = type.GetField(value.ToString());
            if (fieldInfo == null)
                return null;

            StringValueAttribute[]? attribs = fieldInfo.GetCustomAttributes(typeof(StringValueAttribute), false) as StringValueAttribute[];
            if (attribs == null)
                return null;

            // Return the first if there was a match.
            return attribs.Length > 0 ? attribs[0].StringValue : null;
        }
    }
}

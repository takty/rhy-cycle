#nullable enable

using System;
using System.Collections.Generic;

namespace CadentCable.Abstractions
{
    public enum CCJsonValueKind
    {
        Undefined,
        Null,
        Boolean,
        Number,
        String,
        Object,
        Array,
    }

    public sealed class CCJsonException : Exception
    {
        public CCJsonException(string code, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code;
        }

        public string Code { get; }
    }

    public interface ICCJsonObject
    {
        string RawJson { get; }

        bool HasProperty(string propertyName);
        CCJsonValueKind GetValueKind(string propertyName);
        bool TryGetString(string propertyName, out string value);
        IReadOnlyList<ICCJsonObject> GetObjectArray(string propertyName);
        T ToObject<T>();
    }

    public interface IJsonSerializer
    {
        string Serialize<T>(T value);
        ICCJsonObject ParseObject(string json);
    }
}

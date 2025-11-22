using System;

namespace OSK.Bindings
{
    public enum From
    {
        Self,
        Children,
        Parent,
        Scene,
        Resources,
        StaticMethod,
        Method
    }

    public enum FindBy
    {
        Tag,
        Type,
        Name
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class BindAttribute : Attribute
    {
        // Primary mode groups
        public From From;
        // Optional scene find mode (used with Scene or other find behaviors)
        public FindBy? FindMode;

        // parameters
        public string Tag;            // for FindBy.Tag
        public string Name;           // for FindBy.Name / FindChildByName usage
        public string ResourcePath;   // for Resources
        public Type StaticType;       // for StaticMethod
        public string MethodName;     // for StaticMethod or Method
        public bool IncludeInactive = false;   // when searching children
        public bool AllowNull = false;         // if true, missing is tolerated

        // convenience ctors
        public BindAttribute(From from = From.Self)
        {
            From = from;
        }

        // allow specifying FindBy inline
        public BindAttribute(From from = From.Self, FindBy findBy = FindBy.Type)
        {
            From = from;
            FindMode = findBy;
        }
    }
}
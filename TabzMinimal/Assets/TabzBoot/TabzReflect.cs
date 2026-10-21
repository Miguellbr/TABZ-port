using System;
using System.Reflection;

namespace TabzMinimal
{
    /// Everything that touches the ORIGINAL game classes goes through reflection, so a missing or renamed member
    /// shows up in the in-game log instead of breaking the build.
    internal static class TabzReflect
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        public static Type FindType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); }
                catch (Exception) { }
                if (t != null) return t;
            }
            return null;
        }

        public static object GetMember(Type type, object target, string name)
        {
            PropertyInfo p = type.GetProperty(name, All);
            if (p != null && p.CanRead) return p.GetValue(target, null);
            FieldInfo f = type.GetField(name, All);
            if (f != null) return f.GetValue(target);
            throw new MissingMemberException(type.FullName, name);
        }

        public static void SetMember(Type type, object target, string name, object value)
        {
            PropertyInfo p = type.GetProperty(name, All);
            if (p != null && p.CanWrite) { p.SetValue(target, value, null); return; }
            FieldInfo f = type.GetField(name, All);
            if (f != null) { f.SetValue(target, value); return; }
            throw new MissingMemberException(type.FullName, name);
        }

        public static object Call(Type type, object target, string name, params object[] args)
        {
            foreach (MethodInfo m in type.GetMethods(All))
            {
                if (m.Name != name) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != args.Length) continue;
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (args[i] == null)
                    {
                        if (ps[i].ParameterType.IsValueType) { ok = false; break; }
                        continue;
                    }
                    if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { ok = false; break; }
                }
                if (!ok) continue;
                return m.Invoke(m.IsStatic ? null : target, args);
            }
            throw new MissingMethodException(type.FullName, name + "/" + args.Length);
        }
    }
}

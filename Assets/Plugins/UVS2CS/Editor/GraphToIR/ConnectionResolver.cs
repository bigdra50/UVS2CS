using System.Reflection;
using Unity.VisualScripting;

namespace UVS2CS.GraphToIR
{
    /// <summary>
    /// Define() 失敗した Unit の接続情報を、リフレクション経由で UnitConnection の
    /// protected フィールドから取得するユーティリティ。
    /// </summary>
    public static class ConnectionResolver
    {
        static readonly PropertyInfo SourceUnitProp;
        static readonly PropertyInfo SourceKeyProp;
        static readonly PropertyInfo DestUnitProp;
        static readonly PropertyInfo DestKeyProp;

        static ConnectionResolver()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            // UnitConnection<TSource, TDest> の protected プロパティ
            // InvalidConnection : UnitConnection<IUnitOutputPort, IUnitInputPort>
            var baseType = typeof(InvalidConnection).BaseType;
            if (baseType != null)
            {
                SourceUnitProp = baseType.GetProperty("sourceUnit", flags);
                SourceKeyProp = baseType.GetProperty("sourceKey", flags);
                DestUnitProp = baseType.GetProperty("destinationUnit", flags);
                DestKeyProp = baseType.GetProperty("destinationKey", flags);
            }
        }

        public static bool TryGetSourceInfo(InvalidConnection conn, out IUnit unit, out string key)
        {
            unit = null;
            key = null;
            if (SourceUnitProp == null || SourceKeyProp == null) return false;

            unit = SourceUnitProp.GetValue(conn) as IUnit;
            key = SourceKeyProp.GetValue(conn) as string;
            return unit != null && key != null;
        }

        public static bool TryGetDestInfo(InvalidConnection conn, out IUnit unit, out string key)
        {
            unit = null;
            key = null;
            if (DestUnitProp == null || DestKeyProp == null) return false;

            unit = DestUnitProp.GetValue(conn) as IUnit;
            key = DestKeyProp.GetValue(conn) as string;
            return unit != null && key != null;
        }
    }
}

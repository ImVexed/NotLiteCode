using NotLiteCode.Network;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NotLiteCode
{
    public static class Helpers
    {
        public static bool TryParseEnum<T>(this object SourceObject, out T EnumValue)
        {
            if (!Enum.IsDefined(typeof(T), SourceObject))
            {
                EnumValue = default(T);
                return false;
            }

            EnumValue = (T)Enum.Parse(typeof(T), SourceObject.ToString());
            return true;
        }

        public static void Start<T>(this EventHandler<T> Event, object Source, T Data)
        {
            Task.Run(() => Event(Source, Data));
            //Event.BeginInvoke(Source, Data, (x) =>
            //{
            //    try
            //    {
            //        Event.EndInvoke(x);
            //    }
            //    catch { }
            //}, null);
        }

        public class TiedEventWait
        {
            public EventWaitHandle Event = new EventWaitHandle(false, EventResetMode.ManualReset);
            public NetworkEvent Result;
        }
    }
}
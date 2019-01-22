using NotLiteCode.Network;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
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

        public static async Task<object> InvokeWrapper(this Func<object, object[], object> Method, bool HasAsyncResult, object Target, params object[] Args)
        {
            var Result = Method(Target, Args);
            if (!(Result is Task task))
                return Result;

            if (!task.IsCompleted)
                await task.ConfigureAwait(false);

            return HasAsyncResult ? task.GetType().GetProperty("Result")?.GetValue(task) : null;
        }

        public static Func<object, object[], object> CreateMethodWrapper(Type type, MethodInfo method)
        {
            var paramsExps = CreateParamsExpressions(method, out ParameterExpression argsExp);

            var targetExp = Expression.Parameter(typeof(object));
            var castTargetExp = Expression.Convert(targetExp, type);
            var invokeExp = Expression.Call(castTargetExp, method, paramsExps);

            LambdaExpression lambdaExp;

            if (method.ReturnType != typeof(void))
            {
                var resultExp = Expression.Convert(invokeExp, typeof(object));
                lambdaExp = Expression.Lambda(resultExp, targetExp, argsExp);
            }
            else
            {
                var constExp = Expression.Constant(null, typeof(object));
                var blockExp = Expression.Block(invokeExp, constExp);
                lambdaExp = Expression.Lambda(blockExp, targetExp, argsExp);
            }

            var lambda = lambdaExp.Compile();
            return (Func<object, object[], object>)lambda;
        }

        private static Expression[] CreateParamsExpressions(MethodBase method, out ParameterExpression argsExp)
        {
            var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();

            argsExp = Expression.Parameter(typeof(object[]));
            var paramsExps = new Expression[parameters.Count()];

            for (var i = 0; i < parameters.Count(); i++)
            {
                var constExp = Expression.Constant(i, typeof(int));
                var argExp = Expression.ArrayIndex(argsExp, constExp);
                paramsExps[i] = Expression.Convert(argExp, parameters[i]);
            }

            return paramsExps;
        }

        public static void Start<T>(this EventHandler<T> Event, object Source, T Data)
        {
            Task.Run(() => Event(Source, Data));
        }

        public class TiedEventWait
        {
            public EventWaitHandle Event = new EventWaitHandle(false, EventResetMode.ManualReset);
            public NetworkEvent Result;
        }
    }
}
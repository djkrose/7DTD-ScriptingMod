using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class TempTests
    {
        private readonly MethodInfo serializeObjectMethod = typeof(UnityEngine.Logger).Assembly.GetType("SimpleJson.SimpleJson", true)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(mi => mi.Name == "SerializeObject" && mi.GetParameters().Length == 1);

        private const int Iterations = 1000;

        [Test]
        public void JsonUtilityTest()
        {
            int entityId = 123;
            string entityName = "FatJoe";

            var sw = new MicroStopwatch(true);
            string lastOutput = "";
            for (int i = 0; i < Iterations; i++)
            {
                lastOutput = UnityEngine.JsonUtility.ToJson(new {entityId, entityName});
            }
            Console.WriteLine("[" + sw.ElapsedMicroseconds + "µs per " + Iterations + "] JsonUtility with anon object: " + lastOutput);

            //Console.WriteLine("JsonUtility with complex object: " + UnityEngine.JsonUtility.ToJson(entity));
        }

        [Test]
        public void SimpleJsonTest()
        {
            int entityId = 123;
            string entityName = "FatJoe";

            var sw = new MicroStopwatch(true);
            string lastOutput = "";
            for (int i = 0; i < Iterations; i++)
            {
                lastOutput = SimpleJsonSerialize(new { entityId, entityName });
            }
            Console.WriteLine("[" + sw.ElapsedMicroseconds + "µs per " + Iterations + "] SimpleJson with anon object: " + lastOutput);

            //Console.WriteLine("SimpleJson with complex object: " + SimpleJsonSerialize(entity));
        }

        [Test]
        public void LitJsonTest()
        {
            int entityId = 123;
            string entityName = "FatJoe";

            //var litJson = new LitJson.JsonWriter();

            var sw = new MicroStopwatch(true);
            string lastOutput = "";
            for (int i = 0; i < Iterations; i++)
            {
                lastOutput = LitJson.JsonMapper.ToJson(new { entityId, entityName });
            }
            Console.WriteLine("[" + sw.ElapsedMicroseconds + "µs per " + Iterations + "] LitJson with anon object: " + lastOutput);

            //Console.WriteLine("LitJson with complex object: " + LitJson.JsonMapper.ToJson(entity));
        }

        private string SimpleJsonSerialize(object obj)
        {
            return (string)serializeObjectMethod.Invoke(null, new object[] {obj} );
        }

    }
}

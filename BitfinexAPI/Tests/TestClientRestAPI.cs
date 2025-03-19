using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitfinexAPI.Tests
{
    [TestClass]
    public class TestClientRestAPI
    {
        private BitfinexClientRestAPI client1;
        private BitfinexClientRestAPI client2;

        [TestInitialize]
        public void Setup()
        {
            client1 = new BitfinexClientRestAPI();
            client2 = new BitfinexClientRestAPI();
        }
        [TestMethod]

        public void GetCandleJsons(string candle, string section, Dictionary<string, string?>? parameters)
        {

        }
    }
}

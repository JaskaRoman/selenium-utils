﻿using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Riganti.Utils.Testing.Selenium.Core.Abstractions.Exceptions;

namespace Riganti.Utils.Testing.Selenium.Core.UnitTests
{
    [TestClass]
    public class ExceptionsTests
    {
        private const string UrlConst = "https://example.com/";

        public TestContext TestContext { get; set; }
        [TestMethod]
        public void SeleniumTestFailedExceptionTest()

        {
            var exps = new List<Exception>() { new Exception("Exception1"), new Exception("Exception2"), new Exception("Exception3") };
            var exp = new SeleniumTestFailedException(exps);
            var str = exp.ToString();

            TestContext.WriteLine(str);

            Assert.IsTrue(str.Contains("Exception1"));
            Assert.IsTrue(str.Contains("Exception2"));
            Assert.IsTrue(str.Contains("Exception3"));
            Assert.IsTrue(str.Contains("SpecificBrowserName"));
            Assert.IsTrue(str.Contains("SpecificScreen"));
            Assert.IsTrue(str.Contains(UrlConst));
        }

    }

}
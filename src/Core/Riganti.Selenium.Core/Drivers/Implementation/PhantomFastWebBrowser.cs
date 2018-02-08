﻿using OpenQA.Selenium;
using Riganti.Selenium.Core.Factories;

namespace Riganti.Selenium.Core.Drivers.Implementation
{
    public class PhantomFastWebBrowser : FastWebBrowserBase
    {
        public new LocalWebBrowserFactory Factory => (LocalWebBrowserFactory)base.Factory;

        public PhantomFastWebBrowser(LocalWebBrowserFactory factory) : base(factory)
        {
        }

        protected override IWebDriver CreateDriver()
        {
            return PhantomHelpers.CreatePhantomDriver(Factory);
        }
    }
}

﻿using OpenQA.Selenium;
using Riganti.Utils.Testing.Selenium.Core.Exceptions;
using System;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Threading;
using OpenQA.Selenium.Interactions;
using Riganti.Utils.Testing.Selenium.Core.Api;
using Riganti.Utils.Testing.Selenium.Core.Configuration;
using Riganti.Utils.Testing.Selenium.Core.Drivers;
using Riganti.Utils.Testing.Selenium.Core;
using Riganti.Utils.Testing.Selenium.Core.Abstractions;
using Riganti.Utils.Testing.Selenium.Validators.Checkers.ElementWrapperCheckers;

namespace Riganti.Utils.Testing.Selenium.Core
{
    public class BrowserWrapper :IBrowserWrapper
    {

        private readonly IWebBrowser browser;
        private readonly IWebDriver driver;
        private readonly ITestInstance testInstance;

        public int ActionWaitTime { get; set; }

        public string BaseUrl => testInstance.TestConfiguration.BaseUrl;


        /// <summary>
        /// Generic representation of browser driver.
        /// </summary>
        public IWebDriver Driver
        {
            get
            {
                ActivateScope();
                return driver;
            }
        }

        private ScopeOptions ScopeOptions { get; set; }

        public BrowserWrapper(IWebBrowser browser, IWebDriver driver, ITestInstance testInstance, ScopeOptions scope) 
        {
            this.browser = browser;
            this.driver = driver;

            this.testInstance = testInstance;
            ActionWaitTime = browser.Factory?.TestSuiteRunner?.Configuration.TestRunOptions.ActionTimeout ?? 250;

            ScopeOptions = scope;
            SetCssSelector();
        }
        /// <summary>
        /// Sets implicit timeouts for page load and the time range between actions.
        /// </summary>
        public void SetTimeouts(TimeSpan pageLoadTimeout, TimeSpan implicitlyWait)
        {
            var timeouts = Driver.Manage().Timeouts();
            timeouts.PageLoad = pageLoadTimeout;
            timeouts.ImplicitWait = implicitlyWait;
        }

        private Func<string, By> selectMethodFunc;

        public virtual Func<string, By> SelectMethod
        {
            get { return selectMethodFunc; }
            set
            {
                if (value == null)
                { throw new ArgumentException("SelectMethod cannot be null. This method is used to select elements from loaded page."); }
                selectMethodFunc = value;
            }
        }

        public void SetCssSelector()
        {
            selectMethodFunc = By.CssSelector;
        }

        /// <summary>
        /// Url of active browser tab.
        /// </summary>
        public string CurrentUrl => Driver.Url;

        /// <summary>
        /// Gives path of url of active browser tab.
        /// </summary>
        public string CurrentUrlPath => new Uri(CurrentUrl).GetLeftPart(UriPartial.Path);

        /// <summary>
        /// Compares url with current url of browser.
        /// </summary>
        public bool CompareUrl(string url)
        {
            Uri uri1 = new Uri(url);
            Uri uri2 = new Uri(Driver.Url);

            var result = Uri.Compare(uri1, uri2,
                UriComponents.Scheme | UriComponents.Host | UriComponents.PathAndQuery,
                UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase);

            return result == 0;
        }

        /// <summary>
        /// Compates current Url and given url.
        /// </summary>
        /// <param name="url">This url is compared with CurrentUrl.</param>
        /// <param name="urlKind">Determine whether url parameter contains relative or absolute path.</param>
        /// <param name="components">Determine what parts of urls are compared.</param>
        public bool CompareUrl(string url, UrlKind urlKind, params UriComponents[] components)
        {
            var currentUri = new Uri(CurrentUrl);
            //support relative domain
            //(new Uri() cannot parse the url correctly when the host is missing
            if (urlKind == UrlKind.Relative)
            {
                url = url.StartsWith("/") ? $"{currentUri.Scheme}://{currentUri.Host}{url}" : $"{currentUri.Scheme}://{currentUri.Host}/{url}";
            }

            if (urlKind == UrlKind.Absolute && url.StartsWith("//"))
            {
                if (!string.IsNullOrWhiteSpace(currentUri.Scheme))
                {
                    url = currentUri.Scheme + ":" + url;
                }
            }

            var expectedUri = new Uri(url, UriKind.Absolute);

            if (components.Length == 0)
            {
                throw new BrowserLocationException($"Function CheckUrlCheckUrl(string, UriKind, params UriComponents) has to have one UriComponents at least.");
            }
            UriComponents finalComponent = components[0];
            components.ToList().ForEach(s => finalComponent |= s);

            return Uri.Compare(currentUri, expectedUri, finalComponent, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// Clicks on element.
        /// </summary>
        public IBrowserWrapper  Click(string selector)
        {
            First(selector).Click();
            Wait();
            return this;
        }

        /// <summary>
        /// Submits this element to the web server.
        /// </summary>
        /// <remarks>
        /// If this current element is a form, or an element within a form,
        ///             then this will be submitted to the web server. If this causes the current
        ///             page to change, then this method will block until the new page is loaded.
        /// </remarks>
        public IBrowserWrapper  Submit(string selector)
        {
            First(selector).Submit();
            Wait();
            return this;
        }

        /// <summary>
        /// Navigates to specific url.
        /// </summary>
        /// <param name="url">url to navigate </param>
        /// <remarks>
        /// If url is ABSOLUTE, browser is navigated directly to url.
        /// If url is RELATIVE, browser is navigated to url combined from base url and relative url.
        /// Base url is specified in test configuration. (This is NOT url host of current page!)
        /// </remarks>
        public void NavigateToUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                if (string.IsNullOrWhiteSpace(BaseUrl))
                {
                    throw new InvalidRedirectException();
                }
                LogVerbose($"Start navigation to: {BaseUrl}");
                Driver.Navigate().GoToUrl(BaseUrl);
                return;
            }
            //redirect if is absolute
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                LogVerbose($"Start navigation to: {url}");
                Driver.Navigate().GoToUrl(url);
                return;
            }
            //redirect absolute with same schema
            if (url.StartsWith("//"))
            {
                var schema = new Uri(CurrentUrl).Scheme;
                var navigateUrltmp = $"{schema}:{url}";
                LogVerbose($"Start navigation to: {navigateUrltmp}");
                Driver.Navigate().GoToUrl(navigateUrltmp);
                return;
            }
            var builder = new UriBuilder(BaseUrl);

            // replace url fragments
            if (url.StartsWith("/"))
            {
                builder.Path = "";
                var urlToNavigate = builder.ToString().TrimEnd('/') + "/" + url.TrimStart('/');
                LogVerbose($"Start navigation to: {urlToNavigate}");
                Driver.Navigate().GoToUrl(urlToNavigate);
                return;
            }

            var navigateUrl = builder.ToString().TrimEnd('/') + "/" + url.TrimStart('/');
            LogVerbose($"Start navigation to: {navigateUrl}");
            Driver.Navigate().GoToUrl(navigateUrl);
        }

        public void LogVerbose(string message)
        {
            browser.Factory.LogVerbose($"(#{Thread.CurrentThread.ManagedThreadId}) {message}");
        }

        public void LogInfo(string message)
        {
            browser.Factory.LogInfo($"(#{Thread.CurrentThread.ManagedThreadId}) {message}");
        }

        public void LogError(string message, Exception ex)
        {
            browser.Factory.LogError(new Exception($"(#{Thread.CurrentThread.ManagedThreadId}) {message}", ex));
        }

        /// <summary>
        /// Redirects to base url specified in test configuration
        /// </summary>
        public void NavigateToUrl()
        {
            NavigateToUrl(null);
        }

        /// <summary>
        /// Redirects to page back in Browser history
        /// </summary>
        public void NavigateBack()
        {
            Driver.Navigate().Back();
        }

        /// <summary>
        /// Redirects to page forward in Browser history
        /// </summary>
        public void NavigateForward()
        {
            Driver.Navigate().Forward();
        }

        /// <summary>
        /// Reloads current page.
        /// </summary>
        public void Refresh()
        {
            Driver.Navigate().Refresh();
        }

        /// <summary>
        /// Forcibly ends test.
        /// </summary>
        /// <param name="message">Test failure message</param>
        public void DropTest(string message)
        {
            throw new WebDriverException($"Test forcibly dropped: {message}");
        }

        public string GetAlertText()
        {
            var alert = GetAlert();
            return alert?.Text;
        }

        public IBrowserWrapper  CheckIfAlertTextEquals(string expectedValue, bool caseSensitive = false, bool trim = true)
        {
            var alert = GetAlert();
            var alertText = "";
            if (trim)
            {
                alertText = alert.Text?.Trim();
                expectedValue = expectedValue.Trim();
            }

            if (!string.Equals(alertText, expectedValue,
                    caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                throw new AlertException($"Alert does not contain expected value. Expected value: '{expectedValue}', provided value: '{alertText}'");
            }
            return this;
        }

        public bool HasAlert()
        {
            try
            {
                GetAlert();
            }
            catch
            {
                return false;
            }
            return true;
        }

        public IAlert GetAlert()
        {
            IAlert alert;
            try

            {
                alert = Driver.SwitchTo().Alert();
            }
            catch (Exception ex)
            {
                throw new AlertException("Alert not visible.", ex);
            }
            if (alert == null)
                throw new AlertException("Alert not visible.");
            return alert;
        }

        /// <summary>
        /// Checks if modal dialog (Alert) contains specified text as a part of provided text from the dialog.
        /// </summary>
        public IBrowserWrapper  CheckIfAlertTextContains(string expectedValue, bool trim = true)
        {
            var alert = GetAlert();
            var alertText = "";
            if (trim)
            {
                alertText = alert.Text?.Trim();
                expectedValue = expectedValue.Trim();
            }

            if (alertText == null || !alertText.Contains(expectedValue))
            {
                throw new AlertException($"Alert does not contain expected value. Expected value: '{expectedValue}', provided value: '{alertText}'");
            }
            return this;
        }

        /// <summary>
        /// Checks if modal dialog (Alert) text equals with specified text.
        /// </summary>
        public IBrowserWrapper  CheckIfAlertText(Func<string, bool> expression, string failureMessage = "")
        {
            var alert = Driver.SwitchTo().Alert()?.Text;
            if (!expression(alert))
            {
                throw new AlertException($"Alert text is not correct. Provided value: '{alert}' \n { failureMessage } ");
            }
            return this;
        }

        /// <summary>
        /// Confirms modal dialog (Alert).
        /// </summary>
        public IBrowserWrapper  ConfirmAlert()
        {
            Driver.SwitchTo().Alert().Accept();
            Wait();
            return this;
        }

        /// <summary>
        /// Dismisses modal dialog (Alert).
        /// </summary>
        public IBrowserWrapper  DismissAlert()
        {
            Driver.SwitchTo().Alert().Dismiss();
            Wait();
            return this;
        }

        /// <summary>
        /// Waits specified time in milliseconds.
        /// </summary>
        public IBrowserWrapper  Wait(int milliseconds)
        {
            Thread.Sleep(milliseconds);
            return this;
        }

        /// <summary>
        /// Waits time specified by ActionWaitType property.
        /// </summary>
        public IBrowserWrapper  Wait()
        {
            return Wait(ActionWaitTime);
        }

        /// <summary>
        /// Waits specified time.
        /// </summary>
        public IBrowserWrapper  Wait(TimeSpan interval)
        {
            Thread.Sleep((int)interval.TotalMilliseconds);
            return this;
        }

        /// <summary>
        /// Finds all elements that satisfy the condition of css selector.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        public IElementWrapperCollection FindElements(By selector)
        {
            return Driver.FindElements(selector).ToElementsList(this, selector.GetSelector());
        }

        /// <summary>
        /// Finds all elements that satisfy the condition of css selector.
        /// </summary>
        /// <param name="cssSelector"></param>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public IElementWrapperCollection FindElements(string cssSelector, Func<string, By> tmpSelectMethod = null)
        {
            return Driver.FindElements((tmpSelectMethod ?? SelectMethod)(cssSelector)).ToElementsList(this, (tmpSelectMethod ?? SelectMethod)(cssSelector).GetSelector());
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IElementWrapper FirstOrDefault(string selector, Func<string, By> tmpSelectMethod = null)
        {
            var elms = FindElements(selector, tmpSelectMethod);
            return elms.FirstOrDefault();
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IElementWrapper First(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return ThrowIfIsNull(FirstOrDefault(selector, tmpSelectMethod), $"Element not found. Selector: {selector}");
        }

        /// <summary>
        /// Performs specified action on each element from a sequence.
        /// </summary>
        /// <param name="selector">Selector to find a sequence of elements.</param>
        /// <param name="action">Action to perform on each element of a sequence.</param>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public IBrowserWrapper  ForEach(string selector, Action<IElementWrapper> action, Func<string, By> tmpSelectMethod = null)
        {
            FindElements(selector, tmpSelectMethod).ForEach(action);
            return this;
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IElementWrapper SingleOrDefault(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).SingleOrDefault();
        }


        /// <summary>
        /// Returns one element and throws exception when no element or more then one element is found.
        /// </summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IElementWrapper Single(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).Single();
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public bool IsDisplayed(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).All(s => s.IsDisplayed());
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IElementWrapperCollection CheckIfIsDisplayed(string selector, Func<string, By> tmpSelectMethod = null)
        {
            var collection = FindElements(selector, tmpSelectMethod);
            var result = collection.ThrowIfSequenceEmpty().All(s => s.IsDisplayed());
            if (!result)
            {
                var index = collection.IndexOf(collection.First(s => !s.IsDisplayed()));
                throw new UnexpectedElementStateException($"One or more elements are not displayed. Selector '{selector}', Index of non-displayed element: {index}");
            }
            return collection;
        }

        ///<summary>Provides elements that satisfies the selector condition at specific position.</summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public IElementWrapperCollection CheckIfIsNotDisplayed(string selector, Func<string, By> tmpSelectMethod = null)
        {
            var collection = FindElements(selector, tmpSelectMethod);
            var result = collection.All(s => s.IsDisplayed()) && collection.Any();
            if (result)
            {
                var index = collection.Any() ? collection.IndexOf(collection.First(s => !s.IsDisplayed())) : -1;
                throw new UnexpectedElementStateException($"One or more elements are displayed and they shouldn't be. Selector '{selector}', Index of non-displayed element: {index}");
            }
            return collection;
        }

        ///<summary>Provides elements that satisfies the selector condition at specific position.</summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IElementWrapper ElementAt(string selector, int index, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).ElementAt(index);
        }

        ///<summary>Provides the last element that satisfies the selector condition.</summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IElementWrapper Last(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).Last();
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public IElementWrapper LastOrDefault(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).LastOrDefault();
        }

        public IBrowserWrapper  FireJsBlur()
        {
            GetJavaScriptExecutor()?.ExecuteScript("if(document.activeElement && document.activeElement.blur) {document.activeElement.blur()}");
            return this;
        }

        public IJavaScriptExecutor GetJavaScriptExecutor()
        {
            return Driver as IJavaScriptExecutor;
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IBrowserWrapper  SendKeys(string selector, string text, Func<string, By> tmpSelectMethod = null)
        {
            FindElements(selector, tmpSelectMethod).ForEach(s => { s.SendKeys(text); s.Wait(); });
            return this;
        }

        /// <summary>
        /// Removes content from selected elements
        /// </summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public IBrowserWrapper  ClearElementsContent(string selector, Func<string, By> tmpSelectMethod = null)
        {
            FindElements(selector, tmpSelectMethod).ForEach(s => { s.Clear(); s.Wait(); });
            return this;
        }

        /// <summary>
        /// Throws exception when provided object is null
        /// </summary>
        /// <param name="obj">Tested object</param>
        /// <param name="message">Failure message</param>
        public T ThrowIfIsNull<T>(T obj, string message)
        {
            if (obj == null)
            {
                throw new NoSuchElementException(message);
            }
            return obj;
        }

        /// <summary>
        /// Takes a screenshot and returns a full path to the file.
        /// </summary>
        ///<param name="filename">Path where the screenshot is going to be saved.</param>
        ///<param name="format">Default value is PNG.</param>
        public void TakeScreenshot(string filename, ScreenshotImageFormat? format = null)
        {
            ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile(filename, format ?? ScreenshotImageFormat.Png);
        }

        /// <summary>
        /// Closes the current browser
        /// </summary>
        public void Dispose()
        {
            Driver.Quit();
            Driver.Dispose();
        }

        #region CheckUrl

        /// <summary>
        /// Checks exact match with CurrentUrl
        /// </summary>
        /// <param name="url">This url is compared with CurrentUrl.</param>
        public IBrowserWrapper  CheckUrlEquals(string url)
        {
            var uri1 = new Uri(CurrentUrl, UriKind.Absolute);
            var uri2 = new Uri(url, UriKind.RelativeOrAbsolute);
            if (uri1 != uri2)
            {
                throw new BrowserLocationException($"Current url is not expected. Current url: '{CurrentUrl}', Expected url: '{url}'.");
            }
            return this;
        }

        /// <summary>
        /// Checks if CurrentUrl satisfies the condition defined by lamda expression
        /// </summary>
        /// <param name="expression">The condition</param>
        /// <param name="failureMessage">Failure message</param>
        public IBrowserWrapper  CheckUrl(Func<string, bool> expression, string failureMessage = null)
        {
            if (!expression(CurrentUrl))
            {
                throw new BrowserLocationException($"Current url is not expected. Current url: '{CurrentUrl}'. " + (failureMessage ?? ""));
            }
            return this;
        }

        /// <summary>
        /// Checks url by its parts
        /// </summary>
        /// <param name="url">This url is compared with CurrentUrl.</param>
        /// <param name="urlKind">Determine whether url parameter contains relative or absolute path.</param>
        /// <param name="components">Determine what parts of urls are compared.</param>
        public IBrowserWrapper  CheckUrl(string url, UrlKind urlKind, params UriComponents[] components)
        {
            if (!CompareUrl(url, urlKind, components))
            {
                throw new BrowserLocationException($"Current url is not expected. Current url: '{CurrentUrl}'. Expected url: '{url}'");
            }
            return this;
        }

        #endregion CheckUrl

        #region FileUploadDialog

        /// <summary>
        /// Opens file dialog and sends keys with full path to file, that should be uploaded.
        /// </summary>
        /// <param name="fileUploadOpener">Element that opens file dialog after it is clicked.</param>
        /// <param name="fullFileName">Full path to file that is intended to be uploaded.</param>
        public virtual IBrowserWrapper  FileUploadDialogSelect(IElementWrapper fileUploadOpener, string fullFileName)
        {
            if (fileUploadOpener.GetTagName() == "input" && fileUploadOpener.HasAttribute("type") && fileUploadOpener.GetAttribute("type") == "file")
            {
                fileUploadOpener.SendKeys(fullFileName);
                Wait();
            }
            else
            {
                // open file dialog
                fileUploadOpener.Click();
                Wait();
                //Another wait is needed because without it sometimes few chars from file path are skipped.
                Wait(1000);
                // write the full path to the dialog
                System.Windows.Forms.SendKeys.SendWait(fullFileName);
                Wait();
                SendEnterKey();
            }
            return this;
        }

        public virtual IBrowserWrapper  SendEnterKey()
        {
            
            System.Windows.Forms.SendKeys.SendWait("{Enter}");
            Wait();
            return this;
        }

        public virtual IBrowserWrapper  SendEscKey()
        {
            System.Windows.Forms.SendKeys.SendWait("{ESC}");
            Wait();
            return this;
        }

        #endregion FileUploadDialog

        #region Frames support

        public IBrowserWrapper  GetFrameScope(string selector)
        {
            var options = new ScopeOptions { FrameSelector = selector, Parent = this, CurrentWindowHandle = Driver.CurrentWindowHandle };
            var iframe = First(selector);
         //AssertUI.CheckIfTagName(iframe, new[] { "iframe", "frame" }, $"The selected element '{iframe.FullSelector}' is not a iframe element.");
            
            iframe.CheckIfTagName(new[] { "iframe", "frame" }, $"The selected element '{iframe.FullSelector}' is not a iframe element.");
            var frame = browser.Driver.SwitchTo().Frame(iframe.WebElement);
            testInstance.TestClass.CurrentScope = options.ScopeId;
            return new BrowserWrapper(browser, frame, testInstance, options);
        }

        #endregion Frames support

        public IBrowserWrapper  CheckIfHyperLinkEquals(string selector, string url, UrlKind kind, params UriComponents[] components)
        {
            ForEach(selector, element =>
                {
                    element.CheckIfHyperLinkEquals(url, kind, components);
                });
            return this;
        }

        /// <summary>
        /// Waits until the condition is true.
        /// </summary>
        /// <param name="condition">Expression that determine whether test should wait or continue</param>
        /// <param name="maxTimeout">If condition is not reached in this timeout (ms) test is dropped.</param>
        /// <param name="failureMessage">Message which is displayed in exception log in case that the condition is not reached</param>
        /// <param name="ignoreCertainException">When StaleElementReferenceException or InvalidElementStateException is thrown than it would be ignored.</param>
        /// <param name="checkInterval">Interval in miliseconds. RECOMMENDATION: let the interval greater than 250ms</param>
        public IBrowserWrapper  WaitFor(Func<bool> condition, int maxTimeout, string failureMessage, bool ignoreCertainException = true, int checkInterval = 500)
        {
            if (condition == null)
            {
                throw new NullReferenceException("Condition cannot be null.");
            }
            var now = DateTime.UtcNow;

            bool isConditionMet = false;
            Exception ex = null;
            do
            {
                try
                {
                    isConditionMet = condition();
                }
                catch (StaleElementReferenceException)
                {
                    if (!ignoreCertainException)
                        throw;
                }
                catch (InvalidElementStateException)
                {
                    if (!ignoreCertainException)
                        throw;
                }

                if (DateTime.UtcNow.Subtract(now).TotalMilliseconds > maxTimeout)
                {
                    throw new WaitBlockException(failureMessage);
                }
                Wait(checkInterval);
            } while (!isConditionMet);
            return this;
        }
        public IBrowserWrapper  WaitFor(Action checkExpression, int maxTimeout, string failureMessage, int checkInterval = 500)
        {
            return WaitFor(() =>
            {
                try
                {
                    checkExpression();
                }
                catch
                {
                    return false;
                }
                return true;
            }, maxTimeout, failureMessage, true, checkInterval);
        }
        /// <summary>
        /// Repeats execution of the action until the action is executed without exception.
        /// </summary>
        /// <param name="maxTimeout">If condition is not reached in this timeout (ms) test is dropped.</param>
        /// <param name="checkInterval">Interval in miliseconds. RECOMMENDATION: let the interval greater than 250ms</param>
        public IBrowserWrapper  WaitFor(Action action, int maxTimeout, int checkInterval = 500, string failureMessage = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            var now = DateTime.UtcNow;

            Exception exceptionThrown = null;
            do
            {
                try
                {
                    action();
                    exceptionThrown = null;
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex;
                }

                if (DateTime.UtcNow.Subtract(now).TotalMilliseconds > maxTimeout)
                {
                    if (failureMessage != null)
                    {
                        throw new WaitBlockException(failureMessage, exceptionThrown);
                    }
                    throw exceptionThrown;
                }
                Wait(checkInterval);
            } while (exceptionThrown != null);
            return this;
        }

        /// <summary>
        /// Checks if browser can access given Url (browser returns status code 2??).
        /// </summary>
        /// <param name="url"></param>
        /// <param name="urlKind"></param>
        /// <returns></returns>
        public IBrowserWrapper  CheckIfUrlIsAccessible(string url, UrlKind urlKind)
        {
            var currentUri = new Uri(CurrentUrl);

            if (urlKind == UrlKind.Relative)
            {
                url = GetAbsoluteUrl(url);
            }

            if (urlKind == UrlKind.Absolute && url.StartsWith("//"))
            {
                if (!string.IsNullOrWhiteSpace(currentUri.Scheme))
                {
                    url = currentUri.Scheme + ":" + url;
                }
            }

            HttpWebResponse response = null;
            LogVerbose($"CheckIfUrlIsAccessible: Checking of url: '{url}'");
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "HEAD";

            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                throw new WebException($"Unable to access {url}! {e.Status}", e);
            }
            finally
            {
                response?.Close();
            }
            return this;
        }

        /// <summary>
        /// Transforms relative Url to absolute. Uses base URL.
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <returns></returns>
        public string GetAbsoluteUrl(string relativeUrl)
        {
            var currentUri = new Uri(BaseUrl);
            return relativeUrl.StartsWith("/") ? $"{currentUri.Scheme}://{currentUri.Host}:{currentUri.Port}{relativeUrl}" : $"{currentUri.Scheme}://{currentUri.Host}:{currentUri.Port}/{relativeUrl}";
        }

        /// <summary>
        /// Switches browser tabs.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IBrowserWrapper  SwitchToTab(int index)
        {
            Driver.SwitchTo().Window(Driver.WindowHandles[index]);
            return this;
        }

        public void ActivateScope()
        {
            if (testInstance.TestClass.CurrentScope == ScopeOptions.ScopeId)
            {
                return;
            }

            if (ScopeOptions.Parent != null && ScopeOptions.Parent != this)
            {
                ScopeOptions.Parent.ActivateScope();
            }
            else
            {
                if (ScopeOptions.CurrentWindowHandle != null && driver.CurrentWindowHandle != ScopeOptions.CurrentWindowHandle)
                {
                    driver.SwitchTo().Window(ScopeOptions.CurrentWindowHandle);
                }
                if (ScopeOptions.Parent == null)
                {
                    driver.SwitchTo().DefaultContent();
                }

                if (ScopeOptions.FrameSelector != null)
                {
                    driver.SwitchTo().Frame(ScopeOptions.FrameSelector);
                }
            }
            testInstance.TestClass.CurrentScope = ScopeOptions.ScopeId;
        }

        public string GetTitle() => Driver.Title;

        public IBrowserWrapper  CheckIfTitleEquals(string title, StringComparison comparison = StringComparison.OrdinalIgnoreCase, bool trim = true)
        {
            var browserTitle = GetTitle();
            if (trim)
            {
                browserTitle = browserTitle.Trim();
                title = title.Trim();
            }

            if (!string.Equals(title, browserTitle, comparison))
            {
                throw new BrowserException($"Provided content in tab's title is not expected. Expected value: '{title}', provided value: '{browserTitle}'");
            }
            return this;
        }

        public IBrowserWrapper  CheckIfTitleNotEquals(string title, StringComparison comparison = StringComparison.OrdinalIgnoreCase, bool trim = true)
        {
            var browserTitle = GetTitle();
            if (trim)
            {
                browserTitle = browserTitle.Trim();
                title = title.Trim();
            }

            if (string.Equals(title, browserTitle, comparison))
            {
                throw new BrowserException($"Provided content in tab's title is not expected. Title should NOT to be equal to '{title}', but provided value is '{browserTitle}'");
            }
            return this;
        }

        public IBrowserWrapper  CheckIfTitle(Func<string, bool> func, string failureMessage = "")
        {
            var browserTitle = GetTitle();

            if (!func(browserTitle))
            {
                throw new BrowserException($"Provided content in tab's title is not expected. Provided content: '{browserTitle}' \r\n{failureMessage}");
            }
            return this;
        }

        /// <summary>
        /// Returns WebDriver without scope activation. Be carefull!!! This is unsecure!
        /// </summary>
        public IWebDriver _GetInternalWebDriver()
        {
            testInstance.TestClass.CurrentScope = Guid.Empty;
            return Driver;
        }

        /// <summary>
        /// Drag on element dragOnElement and drop to dropToElement with offsetX and offsetY.
        /// </summary>
        /// <param name="dragOnElement"></param>
        /// <param name="dropToElement"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <returns></returns>
        public IBrowserWrapper  DragAndDrop(IElementWrapper dragOnElement, IElementWrapper dropToElement, int offsetX = 0, int offsetY = 0)
        {
            var builder = new Actions(_GetInternalWebDriver());
            var from = dragOnElement.WebElement;
            var to = dropToElement.WebElement;
            var dragAndDrop = builder.ClickAndHold(from).MoveToElement(to, offsetX, offsetY).Release(to).Build();
            dragAndDrop.Perform();
            return this;
        }
    }
}
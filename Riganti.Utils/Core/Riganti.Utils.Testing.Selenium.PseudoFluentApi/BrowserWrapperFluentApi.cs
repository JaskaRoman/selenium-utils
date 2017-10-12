﻿using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using Riganti.Utils.Testing.Selenium.Core.Abstractions;
using Riganti.Utils.Testing.Selenium.Core.Abstractions.Exceptions;
using Riganti.Utils.Testing.Selenium.Core.Api;
using Riganti.Utils.Testing.Selenium.Core.Drivers;
using Riganti.Utils.Testing.Selenium.Validators.Checkers;
using Riganti.Utils.Testing.Selenium.Validators.Checkers.BrowserWrapperCheckers;
using Riganti.Utils.Testing.Selenium.Validators.Checkers.ElementWrapperCheckers;

namespace Riganti.Utils.Testing.Selenium.Core
{
    public class BrowserWrapperFluentApi : BrowserWrapper, IBrowserWrapper, IBrowserWrapperFluentApi
    {


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
            return new UrlValidator(url, urlKind, components).CompareUrl(CurrentUrl);
        }

        /// <summary>
        /// Clicks on element.
        /// </summary>
        public IBrowserWrapper Click(string selector)
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
        public IBrowserWrapper Submit(string selector)
        {
            First(selector).Submit();
            Wait();
            return this;
        }


        public string GetAlertText()
        {
            var alert = GetAlert();
            return alert?.Text;
        }

        public IBrowserWrapper CheckIfAlertTextEquals(string expectedValue, bool caseSensitive = false, bool trim = true)
        {
            return EvaluateBrowserCheck<AlertException>(
                new AlertTextEqualsValidator(expectedValue, caseSensitive, trim));
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
        public IBrowserWrapper CheckIfAlertTextContains(string expectedValue, bool trim = true)
        {
            return EvaluateBrowserCheck<AlertException>(new AlertTextContainsValidator(expectedValue, trim));

        }

        /// <summary>
        /// Checks if modal dialog (Alert) text equals with specified text.
        /// </summary>
        public IBrowserWrapper CheckIfAlertText(Expression<Func<string, bool>> expression, string failureMessage = "")
        {
            return EvaluateBrowserCheck<AlertException>(new AlertTextValidator(expression));
        }

        /// <summary>
        /// Confirms modal dialog (Alert).
        /// </summary>
        public IBrowserWrapper ConfirmAlert()
        {
            Driver.SwitchTo().Alert().Accept();
            Wait();
            return this;
        }

        /// <summary>
        /// Dismisses modal dialog (Alert).
        /// </summary>
        public IBrowserWrapper DismissAlert()
        {
            Driver.SwitchTo().Alert().Dismiss();
            Wait();
            return this;
        }



        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IElementWrapperCollection CheckIfIsDisplayed(string selector, Func<string, By> tmpSelectMethod = null)
        {
            var collection = FindElements(selector, tmpSelectMethod);

            var validator = new IsDisplayedValidator();

            var runner = new AllOperationRunner<IElementWrapper>(collection);
            runner.Evaluate<UnexpectedElementStateException>(validator);

            return collection;
        }

        ///<summary>Provides elements that satisfies the selector condition at specific position.</summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public IElementWrapperCollection CheckIfIsNotDisplayed(string selector, Func<string, By> tmpSelectMethod = null)
        {
            var collection = FindElements(selector, tmpSelectMethod);

            var validator = new IsNotDisplayedValidator();

            var runner = new AllOperationRunner<IElementWrapper>(collection);
            runner.Evaluate<UnexpectedElementStateException>(validator);

            return collection;
        }

        public IBrowserWrapper FireJsBlur()
        {
            GetJavaScriptExecutor()?.ExecuteScript("if(document.activeElement && document.activeElement.blur) {document.activeElement.blur()}");
            return this;
        }


        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public IBrowserWrapper SendKeys(string selector, string text, Func<string, By> tmpSelectMethod = null)
        {
            FindElements(selector, tmpSelectMethod).ForEach(s => { s.SendKeys(text); s.Wait(); });
            return this;
        }

        /// <summary>
        /// Removes content from selected elements
        /// </summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public IBrowserWrapper ClearElementsContent(string selector, Func<string, By> tmpSelectMethod = null)
        {
            FindElements(selector, tmpSelectMethod).ForEach(s => { s.Clear(); s.Wait(); });
            return this;
        }

        #region CheckUrl

        /// <summary>
        /// Checks exact match with CurrentUrl
        /// </summary>
        /// <param name="url">This url is compared with CurrentUrl.</param>
        public IBrowserWrapper CheckUrlEquals(string url)
        {
            return EvaluateBrowserCheck<BrowserLocationException>(new UrlEqualsValidator(url));
        }

        /// <summary>
        /// Checks if CurrentUrl satisfies the condition defined by lamda expression
        /// </summary>
        /// <param name="expression">The condition</param>
        /// <param name="failureMessage">Failure message</param>
        public IBrowserWrapper CheckUrl(Expression<Func<string, bool>> expression, string failureMessage = null)
        {
            return EvaluateBrowserCheck<BrowserLocationException>(new CurrentUrlValidator(expression, failureMessage));
        }

        /// <summary>
        /// Checks url by its parts
        /// </summary>
        /// <param name="url">This url is compared with CurrentUrl.</param>
        /// <param name="urlKind">Determine whether url parameter contains relative or absolute path.</param>
        /// <param name="components">Determine what parts of urls are compared.</param>
        public IBrowserWrapper CheckUrl(string url, UrlKind urlKind, params UriComponents[] components)
        {
            return EvaluateBrowserCheck<BrowserLocationException>(new UrlValidator(url, urlKind, components));
        }

        #endregion CheckUrl

        #region FileUploadDialog

        /// <summary>
        /// Opens file dialog and sends keys with full path to file, that should be uploaded.
        /// </summary>
        /// <param name="fileUploadOpener">Element that opens file dialog after it is clicked.</param>
        /// <param name="fullFileName">Full path to file that is intended to be uploaded.</param>
        public virtual IBrowserWrapper FileUploadDialogSelect(IElementWrapper fileUploadOpener, string fullFileName)
        {
            try
            {
                OpenInputFileDialog(fileUploadOpener, fullFileName);
            }
            catch (UnexpectedElementException)
            {
#if net461
                base.OpenFileDialog(fileUploadOpener, fullFileName);
#else
                throw;
#endif
            }

            return this;
        }

    

        #endregion FileUploadDialog



        public IBrowserWrapper CheckIfHyperLinkEquals(string selector, string url, UrlKind kind, params UriComponents[] components)
        {
            var elements = FindElements(selector);
            var validator = new HyperLinkEqualsValidator(url, kind, components);
            var runner = new AllOperationRunner<IElementWrapper>(elements);
            runner.Evaluate<UnexpectedElementStateException>(validator);

            return this;
        }


        /// <summary>
        /// Checks if browser can access given Url (browser returns status code 2??).
        /// </summary>
        /// <param name="url"></param>
        /// <param name="urlKind"></param>
        /// <returns></returns>
        public IBrowserWrapper CheckIfUrlIsAccessible(string url, UrlKind urlKind)
        {
            return EvaluateBrowserCheck<BrowserLocationException>(new UrlIsAccessibleValidator(url, urlKind));
        }

        public string GetTitle() => Driver.Title;

        public IBrowserWrapper CheckIfTitleEquals(string title, StringComparison comparison = StringComparison.OrdinalIgnoreCase, bool trim = true)
        {
            return EvaluateBrowserCheck<BrowserException>(new TitleEqualsValidator(title,
                comparison == StringComparison.Ordinal, trim));
        }

        public IBrowserWrapper CheckIfTitleNotEquals(string title, StringComparison comparison = StringComparison.OrdinalIgnoreCase, bool trim = true)
        {
            return EvaluateBrowserCheck<BrowserException>(new TitleNotEqualsValidator(title,
                comparison == StringComparison.Ordinal, trim));
        }

        public IBrowserWrapper CheckIfTitle(Expression<Func<string, bool>> expression, string failureMessage = "")
        {
            return EvaluateBrowserCheck<BrowserException>(new TitleValidator(expression, failureMessage));
        }

        /// <summary>
        /// Drag on element dragOnElement and drop to dropToElement with offsetX and offsetY.
        /// </summary>
        /// <param name="dragOnElement"></param>
        /// <param name="dropToElement"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <returns></returns>
        public IBrowserWrapper DragAndDrop(IElementWrapper dragOnElement, IElementWrapper dropToElement, int offsetX = 0, int offsetY = 0)
        {
            var builder = new Actions(_GetInternalWebDriver());
            var from = dragOnElement.WebElement;
            var to = dropToElement.WebElement;
            var dragAndDrop = builder.ClickAndHold(from).MoveToElement(to, offsetX, offsetY).Release(to).Build();
            dragAndDrop.Perform();
            return this;
        }

        public BrowserWrapperFluentApi(IWebBrowser browser, IWebDriver driver, ITestInstance testInstance, ScopeOptions scope) : base(browser, driver, testInstance, scope)
        {
        }
    }
}

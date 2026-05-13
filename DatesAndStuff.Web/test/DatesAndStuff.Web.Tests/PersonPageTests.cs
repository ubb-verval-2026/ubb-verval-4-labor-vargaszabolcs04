using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace DatesAndStuff.Web.Tests;

[TestFixture]
public class PersonPageTests
{
    private IWebDriver driver;
    private StringBuilder verificationErrors;
    private const string BaseURL = "http://localhost:5091";
    private bool acceptNextAlert = true;

    private Process? _blazorProcess;

    [OneTimeSetUp]
    public void StartBlazorServer()
    {
        var webProjectPath = Path.GetFullPath(Path.Combine(
            Assembly.GetExecutingAssembly().Location,
            "../../../../../../src/DatesAndStuff.Web/DatesAndStuff.Web.csproj"
            ));

        var webProjFolderPath = Path.GetDirectoryName(webProjectPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{webProjectPath}\" --urls {BaseURL}",
            WorkingDirectory = webProjFolderPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        _blazorProcess = Process.Start(startInfo);

        // Wait for the app to become available
        var client = new HttpClient();
        var timeout = TimeSpan.FromSeconds(30);
        var start = DateTime.Now;

        while (DateTime.Now - start < timeout)
        {
            try
            {
                var result = client.GetAsync(BaseURL).Result;
                if (result.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                Thread.Sleep(1000);
            }
        }
    }

    [OneTimeTearDown]
    public void StopBlazorServer()
    {
        if (_blazorProcess != null && !_blazorProcess.HasExited)
        {
            _blazorProcess.Kill(true);
            _blazorProcess.Dispose();
        }
    }

    [SetUp]
    public void SetupTest()
    {
        driver = new ChromeDriver();
        verificationErrors = new StringBuilder();
    }

    [TearDown]
    public void TeardownTest()
    {
        try
        {
            driver.Quit();
            driver.Dispose();
        }
        catch (Exception)
        {
            // Ignore errors if unable to close the browser
        }
        Assert.That(verificationErrors.ToString(), Is.EqualTo(""));
    }

    [TestCase(0, 5000)]
    [TestCase(5, 5250)]
    [TestCase(10, 5500)]
    [TestCase(-5, 4750)]
    public void Person_SalaryIncrease_ShouldIncrease(double salaryIncreasePercentage, double expectedSalary)
    {
        // Arrange
        driver.Navigate().GoToUrl(BaseURL);

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

        wait.Until(ExpectedConditions.ElementToBeClickable(
            By.XPath("//*[@data-test='PersonPageNavigation']"))).Click();

        var inputBy = By.XPath("//*[@data-test='SalaryIncreasePercentageInput']");
        var submitButtonBy = By.XPath("//*[@data-test='SalaryIncreaseSubmitButton']");
        var salaryLabelBy = By.XPath("//*[@data-test='DisplayedSalary']");

        wait.Until(driver =>
        {
            var input = driver.FindElement(inputBy);
            input.Clear();
            input.SendKeys(salaryIncreasePercentage.ToString(System.Globalization.CultureInfo.InvariantCulture));

            return true;
        });

        // Act
        wait.Until(ExpectedConditions.ElementToBeClickable(submitButtonBy)).Click();

        // Assert
        wait.Until(driver =>
        {
            var salaryText = driver.FindElement(salaryLabelBy).Text;

            return double.TryParse(
                salaryText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var salaryAfterSubmission)
                && Math.Abs(salaryAfterSubmission - expectedSalary) < 0.001;
        });

        var salaryAfterSubmission = double.Parse(
            driver.FindElement(salaryLabelBy).Text,
            System.Globalization.CultureInfo.InvariantCulture);

        salaryAfterSubmission.Should().BeApproximately(expectedSalary, 0.001);
    }

    [Test]
    public void Person_SalaryIncreaseBelowMinusTen_ShouldShowValidationErrors()
    {
        // Arrange
        driver.Navigate().GoToUrl(BaseURL);

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

        wait.Until(ExpectedConditions.ElementToBeClickable(
            By.XPath("//*[@data-test='PersonPageNavigation']"))).Click();

        var inputBy = By.XPath("//*[@data-test='SalaryIncreasePercentageInput']");
        var submitButtonBy = By.XPath("//*[@data-test='SalaryIncreaseSubmitButton']");

        wait.Until(driver =>
        {
            var input = driver.FindElement(inputBy);
            input.Clear();
            input.SendKeys("-11");

            return true;
        });

        // Act
        wait.Until(ExpectedConditions.ElementToBeClickable(submitButtonBy)).Click();

        // Assert
        var expectedErrorMessage = "The specified percentage should be greater than -10.";
        var validationSummaryErrorBy = By.XPath(
            $"//ul[contains(@class, 'validation-errors')]/li[contains(text(), '{expectedErrorMessage}')]");

        var fieldValidationErrorBy = By.XPath(
            $"//*[@data-test='SalaryIncreasePercentageInput']/following-sibling::*[contains(@class, 'validation-message') and contains(text(), '{expectedErrorMessage}')]");

        wait.Until(ExpectedConditions.ElementExists(validationSummaryErrorBy));
        wait.Until(ExpectedConditions.ElementExists(fieldValidationErrorBy));

        var validationSummaryError = driver.FindElement(validationSummaryErrorBy).Text;
        var fieldValidationError = driver.FindElement(fieldValidationErrorBy).Text;

        validationSummaryError.Should().Contain(expectedErrorMessage);
        fieldValidationError.Should().Contain(expectedErrorMessage);
    }

    [Test]
    public void Person_SalaryIncreaseMinusTen_ShouldShowValidationErrorsAndNotUpdateSalary()
    {
        // Arrange
        driver.Navigate().GoToUrl(BaseURL);

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

        wait.Until(ExpectedConditions.ElementToBeClickable(
            By.XPath("//*[@data-test='PersonPageNavigation']"))).Click();

        var inputBy = By.XPath("//*[@data-test='SalaryIncreasePercentageInput']");
        var submitButtonBy = By.XPath("//*[@data-test='SalaryIncreaseSubmitButton']");
        var salaryLabelBy = By.XPath("//*[@data-test='DisplayedSalary']");

        double salaryBeforeSubmission = 0;

        wait.Until(driver =>
        {
            var salaryText = driver.FindElement(salaryLabelBy).Text;

            return double.TryParse(
                salaryText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out salaryBeforeSubmission);
        });

        wait.Until(driver =>
        {
            var input = driver.FindElement(inputBy);
            input.Clear();
            input.SendKeys("-10");

            return true;
        });

        // Act
        wait.Until(ExpectedConditions.ElementToBeClickable(submitButtonBy)).Click();

        // Assert
        var expectedErrorMessage = "The specified percentage should be greater than -10.";

        var validationSummaryErrorBy = By.XPath(
            $"//ul[contains(@class, 'validation-errors')]/li[contains(text(), '{expectedErrorMessage}')]");

        var fieldValidationErrorBy = By.XPath(
            $"//*[@data-test='SalaryIncreasePercentageInput']/following-sibling::*[contains(@class, 'validation-message') and contains(text(), '{expectedErrorMessage}')]");

        wait.Until(ExpectedConditions.ElementExists(validationSummaryErrorBy));
        wait.Until(ExpectedConditions.ElementExists(fieldValidationErrorBy));

        double salaryAfterSubmission = 0;

        wait.Until(driver =>
        {
            var salaryText = driver.FindElement(salaryLabelBy).Text;

            return double.TryParse(
                salaryText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out salaryAfterSubmission);
        });

        salaryAfterSubmission.Should().BeApproximately(salaryBeforeSubmission, 0.001);
    }
    private bool IsElementPresent(By by)
    {
        try
        {
            driver.FindElement(by);
            return true;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }

    private bool IsAlertPresent()
    {
        try
        {
            driver.SwitchTo().Alert();
            return true;
        }
        catch (NoAlertPresentException)
        {
            return false;
        }
    }

    private string CloseAlertAndGetItsText()
    {
        try
        {
            IAlert alert = driver.SwitchTo().Alert();
            string alertText = alert.Text;
            if (acceptNextAlert)
            {
                alert.Accept();
            }
            else
            {
                alert.Dismiss();
            }
            return alertText;
        }
        finally
        {
            acceptNextAlert = true;
        }
    }

    [Test]
    public void BlazeDemo_MexicoCityToDublin_ShouldHaveAtLeastThreeFlights()
    {
        // Arrange
        driver.Navigate().GoToUrl("https://blazedemo.com/");

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

        wait.Until(ExpectedConditions.ElementToBeClickable(
            By.XPath("//select[@name='fromPort']"))).Click();

        wait.Until(ExpectedConditions.ElementToBeClickable(
            By.XPath("//select[@name='fromPort']/option[@value='Mexico City']"))).Click();

        wait.Until(ExpectedConditions.ElementToBeClickable(
            By.XPath("//select[@name='toPort']"))).Click();

        wait.Until(ExpectedConditions.ElementToBeClickable(
            By.XPath("//select[@name='toPort']/option[@value='Dublin']"))).Click();

        // Act
        wait.Until(ExpectedConditions.ElementToBeClickable(
            By.XPath("//input[@value='Find Flights']"))).Click();

        // Assert
        var flightRows = wait.Until(driver =>
        {
            var rows = driver.FindElements(By.XPath("//table/tbody/tr"));
            return rows.Count > 0 ? rows : null;
        });

        flightRows.Count.Should().BeGreaterThanOrEqualTo(3);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Serilog.Events;
using Shouldly;
using Stryker.Core;
using Stryker.Core.Baseline.Providers;
using Stryker.Core.Logging;
using Stryker.Core.Mutators;
using Stryker.Core.Options;
using Stryker.Core.Reporters;
using Xunit;

namespace Stryker.CLI.UnitTest
{

    public class StrykerCLITests
    {
        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        public void ShouldNotStartStryker_WithHelpArgument(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName });

            mock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ShouldThrow_OnException()
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Throws(new Exception("Initial testrun failed")).Verifiable();

            var target = new StrykerCLI(mock.Object);
            Assert.Throws<Exception>(() => target.Run(new string[] { }));
        }

        [Theory]
        [InlineData("--reporters")]
        [InlineData("-r")]
        public void ShouldPassReporterArgumentsToStryker_WithReporterArgument(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, $"['{ Reporter.ConsoleReport }', '{ Reporter.Dots }']" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o =>
                o.ReportersInput.SuppliedInput.Contains(Reporter.ConsoleReport.ToString()) &&
                o.ReportersInput.SuppliedInput.Contains(Reporter.Dots.ToString())
            ), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--excluded-mutations")]
        [InlineData("-em")]
        public void ShouldPassExcludedMutationsArgumentsToStryker_WithExcludedMutationsArgument(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "['string', 'logical']" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o =>
                o.ExcludedMutatorsInput.SuppliedInput.Contains(Mutator.String.ToString()) &&
                o.ExcludedMutatorsInput.SuppliedInput.Contains(Mutator.Logical.ToString())
            ), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData(Mutator.Assignment, "assignment", "assignment statements")]
        [InlineData(Mutator.Arithmetic, "arithmetic", "arithmetic operators")]
        [InlineData(Mutator.Boolean, "boolean", "boolean literals")]
        [InlineData(Mutator.Equality, "equality", "equality operators")]
        [InlineData(Mutator.Linq, "linq", "linq methods")]
        [InlineData(Mutator.Logical, "logical", "logical operators")]
        [InlineData(Mutator.String, "string", "string literals")]
        [InlineData(Mutator.Unary, "unary", "unary operators")]
        [InlineData(Mutator.Update, "update", "update operators")]
        public void ShouldMapToMutatorTypes_ExcludedMutationsNames(Mutator expectedType, params string[] argValues)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            argValues.Count().ShouldBeGreaterThan(0);

            foreach (var argValue in argValues)
            {
                target.Run(new string[] { "-em", $"['{argValue}']" });

                mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.ExcludedMutatorsInput.SuppliedInput.Single() == expectedType.ToString()), It.IsAny<IEnumerable<LogMessage>>()));
            }
        }

        [Theory]
        [InlineData("--project-file")]
        [InlineData("-p")]
        public void ShouldPassProjectArgumentsToStryker_WithProjectArgument(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "SomeProjectName.csproj" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.ProjectUnderTestNameInput.SuppliedInput == "SomeProjectName.csproj"), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--test-projects")]
        [InlineData("-tp")]
        public void ShouldPassTestProjectArgumentsToStryker_WithTestProjectsArgument(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "['TestProjectFolder/SomeTestProjectName.csproj']" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.TestProjectsInput.SuppliedInput.Count() == 1), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--solution-path")]
        [InlineData("-s")]
        public void ShouldPassSolutionArgumentPlusBasePathToStryker_WithSolutionArgument(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "SomeSolutionPath.sln" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.SolutionPathInput.SuppliedInput.Contains("SomeSolutionPath.sln")), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--log-level")]
        [InlineData("-l")]
        public void ShouldPassLogConsoleArgumentsToStryker_WithLogConsoleArgument(string argName)
        {
            StrykerOptions actualOptions = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => actualOptions = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(new[] { argName, "debug" });

            mock.VerifyAll();
            actualOptions.LogOptions.LogLevel.ShouldBe(LogEventLevel.Debug);
            actualOptions.LogOptions.LogToFile.ShouldBeFalse();
        }

        [Theory]
        [InlineData("--log-file")]
        public void ShouldPassLogFileArgumentsToStryker_WithLogLevelFileArgument_(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.LogToFileInput.SuppliedInput.Value), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--dev-mode")]
        public void WithDevModeArgument_ShouldPassDevModeArgumentsToStryker(string argName)
        {
            StrykerOptions actualOptions = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.3);

            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => actualOptions = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName });

            mock.VerifyAll();

            actualOptions.DevMode.ShouldBeTrue();
        }

        [Theory]
        [InlineData("--timeout-ms")]
        [InlineData("-t")]
        public void WithTimeoutArgument_ShouldPassTimeoutToStryker(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "1000" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o =>
                o.AdditionalTimeoutMsInput.SuppliedInput == 1000), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--max-concurrent-test-runners")]
        [InlineData("-c")]
        public void WithMaxConcurrentTestrunnerArgument_ShouldPassValidatedConcurrentTestrunnersToStryker(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "4" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o =>
                o.ConcurrencyInput.SuppliedInput <= 4), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--threshold-break")]
        [InlineData("-tb")]
        public void WithCustomThresholdBreakParameter_ShouldPassThresholdBreakToStryker(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "20" });
            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o =>
                o.ThresholdBreakInput.SuppliedInput == 20), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--threshold-low")]
        [InlineData("-tl")]
        public void WithCustomThresholdLowParameter_ShouldPassThresholdLowToStryker(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "65" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o =>
                o.ThresholdLowInput.SuppliedInput == 65), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--threshold-high")]
        [InlineData("-th")]
        public void WithCustomThresholdHighParameter_ShouldPassThresholdHighToStryker(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var runResult = new StrykerRunResult(options, 0.3);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResult);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "90" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o =>
                o.ThresholdHighInput.SuppliedInput == 90), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Fact]
        public void OnMutationScoreBelowThresholdBreak_ShouldReturnExitCode1()
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions()
            {
                Thresholds = new Thresholds
                {
                    Break = 40
                }
            };
            var strykerRunResult = new StrykerRunResult(options, 0.3);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(strykerRunResult).Verifiable();

            var target = new StrykerCLI(mock.Object);
            var result = target.Run(new string[] { });

            mock.Verify();
            target.ExitCode.ShouldBe(1);
            result.ShouldBe(1);
        }

        [Fact]
        public void OnMutationScoreEqualToNullAndThresholdBreakEqualTo0_ShouldReturnExitCode0()
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions()
            {
                Thresholds = new Thresholds
                {
                    Break = 0
                }
            };
            var strykerRunResult = new StrykerRunResult(options, double.NaN);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(strykerRunResult).Verifiable();

            var target = new StrykerCLI(mock.Object);
            var result = target.Run(new string[] { });

            mock.Verify();
            target.ExitCode.ShouldBe(0);
            result.ShouldBe(0);
        }

        [Fact]
        public void OnMutationScoreEqualToNullAndThresholdBreakAbove0_ShouldReturnExitCode0()
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions()
            {
                Thresholds = new Thresholds
                {
                    Break = 40
                }
            };
            var strykerRunResult = new StrykerRunResult(options, double.NaN);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(strykerRunResult).Verifiable();

            var target = new StrykerCLI(mock.Object);
            var result = target.Run(new string[] { });

            mock.Verify();
            target.ExitCode.ShouldBe(0);
            result.ShouldBe(0);
        }

        [Fact]
        public void OnMutationScoreAboveThresholdBreak_ShouldReturnExitCode0()
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions()
            {
                Thresholds = new Thresholds
                {
                    Break = 0
                }
            };
            var strykerRunResult = new StrykerRunResult(options, 0.1);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(strykerRunResult).Verifiable();

            var target = new StrykerCLI(mock.Object);
            var result = target.Run(new string[] { });

            mock.Verify();
            target.ExitCode.ShouldBe(0);
            result.ShouldBe(0);
        }

        [Fact]
        public void ShouldPassDefaultValueToStryker_WithNoFilesToExcludeSet()
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var strykerRunResult = new StrykerRunResult(options, 0.1);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(() => strykerRunResult);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.MutateInput.SuppliedInput.Count() == 1), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--files-to-exclude")]
        [InlineData("-fte")]
        public void ShouldPassFilesToExcludeToStryker_WithFilesToExcludeSet(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            StrykerOptions actualOptions = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.1);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => actualOptions = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(new[] { argName, @"['./StartUp.cs','./ExampleDirectory/Recursive.cs', './ExampleDirectory/Recursive2.cs']" });

            var firstFileToExclude = FilePattern.Parse("!StartUp.cs");
            var secondFileToExclude = FilePattern.Parse("!ExampleDirectory/Recursive.cs");
            var thirdFileToExclude = FilePattern.Parse("!ExampleDirectory/Recursive2.cs");

            var filePatterns = actualOptions.Mutate.ToArray();
            filePatterns.Count(x => x.IsExclude).ShouldBe(3);
            filePatterns.ShouldContain(firstFileToExclude);
            filePatterns.ShouldContain(secondFileToExclude);
            filePatterns.ShouldContain(thirdFileToExclude);
        }

        [Theory]
        [InlineData("--mutate")]
        [InlineData("-m")]
        public void ShouldPassFilePatternSetToStryker_WithFilePatternSet(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            StrykerOptions actualOptions = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.1);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => actualOptions = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(new[] { argName, @"['**/*Service.cs','!**/MySpecialService.cs', '**/MyOtherService.cs{1..10}{32..45}']" });

            var firstFileToExclude = FilePattern.Parse("**/*Service.cs");
            var secondFileToExclude = FilePattern.Parse("!**/MySpecialService.cs");
            var thirdFileToExclude = FilePattern.Parse("**/MyOtherService.cs{1..10}{32..45}");

            var filePatterns = actualOptions.Mutate.ToArray();
            filePatterns.Length.ShouldBe(3);
            filePatterns.ShouldContain(firstFileToExclude);
            filePatterns.ShouldContain(secondFileToExclude);
            filePatterns.ShouldContain(thirdFileToExclude);
        }

        [Theory]
        [InlineData("--diff")]
        [InlineData("-diff")]
        public void ShouldEnableDiffFeatureWhenPassed(string argName)
        {
            StrykerOptions actualOptions = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.3);

            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => actualOptions = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName });

            mock.VerifyAll();

            actualOptions.Since.ShouldBeTrue();
        }

        [Theory]
        [InlineData("--mutation-level")]
        [InlineData("-level")]
        public void ShouldSetMutationLevelWhenPassed(string argName)
        {
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "advanced" });
            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o =>
                o.MutationLevelInput.SuppliedInput == MutationLevel.Advanced.ToString()), It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--dashboard-compare", "--dashboard-version project")]
        [InlineData("-compare", "-version project")]
        public void ShouldEnableDiffCompareToDashboardFeatureWhenPassed(params string[] argName)
        {
            StrykerOptions options = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.3);

            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => options = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(argName);

            mock.VerifyAll();

            options.WithBaseline.ShouldBeTrue();
        }

        [Theory]
        [InlineData("--dashboard-compare", "--dashboard-version project")]
        [InlineData("-compare", "-version project")]
        public void ShouldEnableDiffFeatureWhenDashboardComparePassed(params string[] argNames)
        {
            StrykerOptions options = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.3);

            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => options = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(argNames);

            mock.VerifyAll();

            options.Since.ShouldBeTrue();
        }

        [Theory]
        [InlineData("--dashboard-url https://www.example.com/")]
        [InlineData("-url https://www.example.com/")]
        public void ShouldOverwriteDefaultDashboardUrlWhenPassed(string argName)
        {
            StrykerOptions options = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.3);

            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => options = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "--reporters", "['dashboard']", "--dashboard-project", "test", "--dashboard-api-key", "test" });

            mock.VerifyAll();

            options.DashboardUrl.ShouldBe("https://www.example.com/");
        }

        [Fact]
        public void ShouldKeepDefaultDashboardUrlWhenArgumentNotProvided()
        {
            StrykerOptions options = null;
            var runResults = new StrykerRunResult(new StrykerOptions(), 0.3);

            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>()))
                .Callback<StrykerOptions, IEnumerable<LogMessage>>((c, m) => options = c)
                .Returns(runResults)
                .Verifiable();

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { "--reporters", "['dashboard']", "--dashboard-project", "test", "--dashboard-api-key", "test" });

            mock.VerifyAll();

            options.DashboardUrl.ShouldBe("https://dashboard.stryker-mutator.io");
        }

        [Theory]
        [InlineData("--git-diff-target")]
        [InlineData("-gdt")]
        public void ShouldSetGitDiffTargetWhenPassed(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName, "development" });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.SinceTargetInput.SuppliedInput == "development"),
                It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--baseline-storage-location disk")]
        [InlineData("-bsl disk")]
        public void ShouldSetDiskBaselineProviderWhenSpecified(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.BaselineProviderInput.SuppliedInput == BaselineProvider.Disk.ToString()),
                It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--baseline-storage-location dashboard")]
        [InlineData("-bsl dashboard")]
        public void ShouldSetDashboardBaselineProviderWhenSpecified(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.BaselineProviderInput.SuppliedInput == BaselineProvider.Dashboard.ToString()),
                It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Fact]
        public void ShouldSetDiskBaselineProviderWhenNotSpecifiedAndNoDashboardReporterSpecified()
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.BaselineProviderInput.SuppliedInput == BaselineProvider.Disk.ToString()),
                It.IsAny<IEnumerable<LogMessage>>()));
        }

        [Theory]
        [InlineData("--diff-ignore-files ['**/*.ts']")]
        [InlineData("-diffignorefiles ['**/*.ts']")]
        public void ShouldCreateDiffIgnoreGlobFiltersIfSpecified(string argName)
        {
            var mock = new Mock<IStrykerRunner>(MockBehavior.Strict);
            var options = new StrykerOptions();
            var runResults = new StrykerRunResult(options, 0.3);

            mock.Setup(x => x.RunMutationTest(It.IsAny<StrykerInputs>(), It.IsAny<IEnumerable<LogMessage>>())).Returns(runResults);

            var target = new StrykerCLI(mock.Object);

            target.Run(new string[] { argName });

            mock.Verify(x => x.RunMutationTest(It.Is<StrykerInputs>(o => o.DiffIgnoreFilePatternsInput.SuppliedInput.Count() == 1),
                It.IsAny<IEnumerable<LogMessage>>()));
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Interactive.CSharpProject.Build;
using Microsoft.DotNet.Interactive.CSharpProject.Servers.Roslyn;
using Pocket;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Interactive.CSharpProject.Tests;

public class RoslynWorkspaceServerConsoleProjectTests : WorkspaceServerTests
{
    public RoslynWorkspaceServerConsoleProjectTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override Workspace CreateWorkspaceWithMainContaining(string text)
    {
        return Workspace.FromSource(
            $@"using System; using System.Linq; using System.Collections.Generic; class Program {{ static void Main() {{ {text}
                    }}
                }}
            ",
            workspaceType: "console");
    }

    [Fact]
    public async Task Run_returns_emoji()
    {
        var server = GetCodeRunner();

        var workspace = new Workspace(
            workspaceType: "console",
            files: new[] { new ProjectFileContent("Program.cs", SourceCodeProvider.ConsoleProgramSingleRegion) },
            buffers: new[] { new Buffer("Program.cs@alpha", @"Console.OutputEncoding = System.Text.Encoding.UTF8;Console.WriteLine(""😊"");", 0) });

        var result = await server.RunAsync(new WorkspaceRequest(workspace));

        result.Should().BeEquivalentTo(new
        {
            Succeeded = true,
            Output = new[] { "😊", "" },
            Exception = (string)null, // we already display the error in Output
        }, config => config.ExcludingMissingMembers());
    }

    [Fact]
    public async Task When_run_fails_to_compile_then_diagnostics_are_aligned_with_buffer_span()
    {
        var server = GetCodeRunner();

        var workspace = new Workspace(
            workspaceType: "console",
            files: new[] { new ProjectFileContent("Program.cs", SourceCodeProvider.ConsoleProgramSingleRegion) },
            buffers: new[] { new Buffer("Program.cs@alpha", @"Console.WriteLine(banana);", 0) });


        var result = await server.RunAsync(new WorkspaceRequest(workspace));

        result.Should().BeEquivalentTo(new
        {
            Succeeded = false,
            Output = new[] { "(1,19): error CS0103: The name \'banana\' does not exist in the current context" },
            Exception = (string)null, // we already display the error in Output
        }, config => config.ExcludingMissingMembers());
    }

    [Fact]
    public async Task When_run_fails_to_compile_then_diagnostics_are_aligned_with_buffer_span_when_code_is_multi_line()
    {
        var server = GetCodeRunner();

        var workspace = new Workspace(
            workspaceType: "console",
            files: new[] { new ProjectFileContent("Program.cs", SourceCodeProvider.ConsoleProgramSingleRegion) },
            buffers: new[] { new Buffer("Program.cs@alpha", @"var a = 10;" + Environment.NewLine + "Console.WriteLine(banana);", 0) });

        var result = await server.RunAsync(new WorkspaceRequest(workspace));

        result.Should().BeEquivalentTo(new
        {
            Succeeded = false,
            Output = new[] { "(2,19): error CS0103: The name \'banana\' does not exist in the current context" },
            Exception = (string)null, // we already display the error in Output
        }, config => config.ExcludingMissingMembers());
    }

    [Fact]
    public async Task When_diagnostics_are_outside_of_viewport_then_they_are_omitted()
    {
        var server = GetCodeRunner();

        var workspace = new Workspace(
            workspaceType: "console",
            files: new[] { new ProjectFileContent("Program.cs", SourceCodeProvider.ConsoleProgramSingleRegionExtraUsing) },
            buffers: new[] { new Buffer("Program.cs@alpha", @"var a = 10;" + Environment.NewLine + "Console.WriteLine(a);", 0) });

        var result = await server.RunAsync(new WorkspaceRequest(workspace));

        result.GetFeature<Diagnostics>().Should().BeEmpty();
    }

    [Fact]
    public async Task When_compile_fails_then_diagnostics_are_aligned_with_buffer_span()
    {
        var server = GetCodeCompiler();

        var workspace = new Workspace(
            workspaceType: "console",
            files: new[] { new ProjectFileContent("Program.cs", SourceCodeProvider.ConsoleProgramSingleRegion) },
            buffers: new[] { new Buffer("Program.cs@alpha", @"Console.WriteLine(banana);", 0) });


        var result = await server.CompileAsync(new WorkspaceRequest(workspace));

        result.Should().BeEquivalentTo(new
        {
            Succeeded = false,
            Output = new[] { "(1,19): error CS0103: The name \'banana\' does not exist in the current context" },
            Exception = (string)null, // we already display the error in Output
        }, config => config.ExcludingMissingMembers());
    }

    [Fact]
    public async Task When_compile_fails_then_diagnostics_are_aligned_with_buffer_span_when_code_is_multi_line()
    {
        var server = GetCodeCompiler();

        var workspace = new Workspace(
            workspaceType: "console",
            files: new[] { new ProjectFileContent("Program.cs", SourceCodeProvider.ConsoleProgramSingleRegion) },
            buffers: new[] { new Buffer("Program.cs@alpha", @"var a = 10;" + Environment.NewLine + "Console.WriteLine(banana);", 0) });

        var result = await server.CompileAsync(new WorkspaceRequest(workspace));

        result.Should().BeEquivalentTo(new
        {
            Succeeded = false,
            Output = new[] { "(2,19): error CS0103: The name \'banana\' does not exist in the current context" },
            Exception = (string)null, // we already display the error in Output
        }, config => config.ExcludingMissingMembers());
    }

    [Fact]
    public async Task When_compile_diagnostics_are_outside_of_viewport_then_they_are_omitted()
    {
        var server = GetCodeCompiler();

        var workspace = new Workspace(
            workspaceType: "console",
            files: new[] { new ProjectFileContent("Program.cs", SourceCodeProvider.ConsoleProgramSingleRegionExtraUsing) },
            buffers: new[] { new Buffer("Program.cs@alpha", @"var a = 10;" + Environment.NewLine + "Console.WriteLine(a);", 0) });

        var result = await server.CompileAsync(new WorkspaceRequest(workspace));

        result.GetFeature<Diagnostics>().Should().BeEmpty();
    }

    [Fact]
    public async Task When_compile_diagnostics_are_outside_of_active_file_then_they_are_omitted()
    {
        #region bufferSources

        const string program = @"
using System;
using System.Linq;

namespace FibonacciTest
{
    public class Program
    {
        public static void Main()
        {
            foreach (var i in FibonacciGenerator.Fibonacci().Take(20))
            {
                Console.WriteLine(i);
            }
        }
    }
}";
        const string generator = @"
using Newtonsoft.Json;
using System.Collections.Generic;

namespace FibonacciTest
{
    public static class FibonacciGenerator
    {
        public  static IEnumerable<int> Fibonacci()
        {
            int current = 1, next = 1;
            while (true)
            {
                yield return current;
                next = current + (current = next);
            }
        }
    }
}";
        #endregion

        var server = GetCodeCompiler();

        var request = new WorkspaceRequest(
            new Workspace(
                workspaceType: "console",
                buffers: new[]
                {
                    new Buffer("Program.cs", program, 0),
                    new Buffer("FibonacciGenerator.cs", generator, 0)
                },
                includeInstrumentation: true),
            new BufferId("Program.cs"));

        var result = await server.CompileAsync(request);

        result.GetFeature<Diagnostics>().Should().BeEmpty();
    }

    [Fact]
    public async Task When_diagnostics_are_outside_of_active_file_then_they_are_omitted()
    {
        #region bufferSources

        const string program = @"
using System.Collections.Generic;
using System.Linq;
using System;

namespace FibonacciTest
{
    public class Program
    {
        public static void Main()
        {
            foreach (var i in FibonacciGenerator.Fibonacci().Take(20))
            {
                Console.WriteLine(i);
            }
        }
    }
}";
        const string generator = @"
using Newtonsoft.Json;
using System.Collections.Generic;

namespace FibonacciTest
{
    public static class FibonacciGenerator
    {
        public  static IEnumerable<int> Fibonacci()
        {
            int current = 1, next = 1;
            while (true)
            {
                yield return current;
                next = current + (current = next);
            }
        }
    }
}";
        #endregion

        var server = GetCodeRunner();

        var request = new WorkspaceRequest(
            new Workspace(
                workspaceType: "console",
                buffers: new[]
                {
                    new Buffer("Program.cs", program, 0),
                    new Buffer("FibonacciGenerator.cs", generator, 0)
                },
                includeInstrumentation: false),
            new BufferId("Program.cs"));

        var result = await server.RunAsync(request);

        result.GetFeature<Diagnostics>().Should().BeEmpty();
    }

    [Fact]
    public async Task When_compile_is_unsuccessful_and_there_are_multiple_buffers_with_errors_then_diagnostics_for_both_buffers_are_displayed_in_output()
    {
        #region bufferSources

        const string programWithCompileError = @"using System;
using System.Linq;

namespace FibonacciTest
{
    public class Program
    {
        public static void Main()
        {
            foreach (var i in FibonacciGenerator.Fibonacci().Take(20))
            {
                Console.WriteLine(i)          DOES NOT COMPILE
            }
        }
    }
}";
        const string generatorWithCompileError = @"using System.Collections.Generic;
namespace FibonacciTest
{
    public static class FibonacciGenerator
    {
        public  static IEnumerable<int> Fibonacci()
        {
            int current = 1, next = 1;        DOES NOT COMPILE
            while (true)
            {
                yield return current;
                next = current + (current = next);
            }
        }
    }
}";
        #endregion

        var server = GetCodeRunner();

        var request = new WorkspaceRequest(
            new Workspace(
                workspaceType: "console",
                buffers: new[]
                {
                    new Buffer("Program.cs", programWithCompileError),
                    new Buffer("FibonacciGenerator.cs", generatorWithCompileError)
                },
                includeInstrumentation: true),
            new BufferId("FibonacciGenerator.cs"));

        var result = await server.RunAsync(request);
        result.Succeeded.Should().BeFalse();

        result.Output
            .Should()
            .BeEquivalentTo(
                "FibonacciGenerator.cs(8,47): error CS0246: The type or namespace name 'DOES' could not be found (are you missing a using directive or an assembly reference?)",
                "FibonacciGenerator.cs(8,56): error CS0103: The name 'COMPILE' does not exist in the current context",
                "FibonacciGenerator.cs(8,56): error CS1002: ; expected",
                "FibonacciGenerator.cs(8,63): error CS1002: ; expected",
                "Program.cs(12,47): error CS1002: ; expected",
                "Program.cs(12,47): error CS0246: The type or namespace name 'DOES' could not be found (are you missing a using directive or an assembly reference?)",
                "Program.cs(12,56): error CS0103: The name 'COMPILE' does not exist in the current context",
                "Program.cs(12,56): error CS1002: ; expected",
                "Program.cs(12,63): error CS1002: ; expected");
    }

    [Fact]
    public async Task When_compile_is_unsuccessful_and_there_are_multiple_masked_buffers_with_errors_then_diagnostics_for_both_buffers_are_displayed_in_output()
    {
        #region bufferSources

        const string programWithCompileError = @"using System;
using System.Linq;

namespace FibonacciTest
{
    public class Program
    {
        public static void Main()
        {
#region mask
            foreach (var i in FibonacciGenerator.Fibonacci().Take(20))
            {
                Console.WriteLine(i);
            }
#endregion
        }
    }
}";
        const string generatorWithCompileError = @"using System.Collections.Generic;
namespace FibonacciTest
{
    public static class FibonacciGenerator
    {
        public static IEnumerable<int> Fibonacci()           
        {
#region mask
            int current = 1, next = 1;
            while (true)
            {
                yield return current;
                next = current + (current = next);
            }
#endregion
        }
    }
}";
        #endregion

        var server = GetCodeRunner();

        var request = new WorkspaceRequest(
            new Workspace(
                workspaceType: "console",

                files: new[]
                {
                    new ProjectFileContent("Program.cs", programWithCompileError),
                    new ProjectFileContent("FibonacciGenerator.cs", generatorWithCompileError),
                },
                buffers: new[]
                {
                    new Buffer("Program.cs@mask", "WAT"),
                    new Buffer("FibonacciGenerator.cs@mask", "HUH"),
                },

                includeInstrumentation: true),
            new BufferId("FibonacciGenerator.cs", "mask2"));

        var result = await server.RunAsync(request);
        result.Succeeded.Should().BeFalse();

        Logger.Log.Info("OUTPUT:\n{output}", result.Output);

        result.Output
            .Should()
            .Contain(line => line.Contains("WAT"))
            .And
            .Contain(line => line.Contains("HUH"));
    }

    [Fact(Skip = "Needs moved onto Package2")]
    public async Task Response_with_multi_buffer_workspace_with_instrumentation()
    {
        #region bufferSources

        const string program = @"using System;
using System.Linq;

namespace FibonacciTest
{
    public class Program
    {
        public static void Main()
        {
            foreach (var i in FibonacciGenerator.Fibonacci().Take(20))
            {
                Console.WriteLine(i);
            }
        }
    }
}";
        const string generator = @"using System.Collections.Generic;

namespace FibonacciTest
{
    public static class FibonacciGenerator
    {
        public  static IEnumerable<int> Fibonacci()
        {
            int current = 1, next = 1;
            while (true)
            {
                yield return current;
                next = current + (current = next);
            }
        }
    }
}";

        #endregion

        var server = GetCodeRunner();

        var request = new WorkspaceRequest(
            new Workspace(
                workspaceType: "console",
                buffers: new[]
                {
                    new Buffer("Program.cs", program, 0),
                    new Buffer("FibonacciGenerator.cs", generator, 0)
                },
                includeInstrumentation: true),
            new BufferId("Program.cs"));

        var result = await server.RunAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Output.Count.Should().Be(21);
        result.Output.Should().BeEquivalentTo("1", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", "144", "233", "377", "610", "987", "1597", "2584", "4181", "6765", "");
    }
    
    [Fact]
    public async Task Response_with_multi_buffer_workspace()
    {
        #region bufferSources

        const string program = @"using System;
using System.Linq;

namespace FibonacciTest
{
    public class Program
    {
        public static void Main()
        {
            foreach (var i in FibonacciGenerator.Fibonacci().Take(20))
            {
                Console.WriteLine(i);
            }
        }
    }
}";
        const string generator = @"using System.Collections.Generic;

namespace FibonacciTest
{
    public static class FibonacciGenerator
    {
        public  static IEnumerable<int> Fibonacci()
        {
            int current = 1, next = 1;
            while (true)
            {
                yield return current;
                next = current + (current = next);
            }
        }
    }
}";
        #endregion

        var server = GetCodeRunner();

        var workspace = new Workspace(workspaceType: "console", buffers: new[]
        {
            new Buffer("Program.cs", program, 0),
            new Buffer("FibonacciGenerator.cs", generator, 0)
        });

        var result = await server.RunAsync(new WorkspaceRequest(workspace, BufferId.Parse("Program.cs")));

        result.Succeeded.Should().BeTrue();
        result.Output.Count.Should().Be(21);
        result.Output.Should().BeEquivalentTo("1", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", "144", "233", "377", "610", "987", "1597", "2584", "4181", "6765", "");
    }

    [Fact]
    public async Task Response_with_multi_buffer_using_relative_paths_workspace()
    {
        #region bufferSources

        const string program = @"using System;
using System.Linq;

namespace FibonacciTest
{
    public class Program
    {
        public static void Main()
        {
            foreach (var i in FibonacciGenerator.Fibonacci().Take(20))
            {
                Console.WriteLine(i);
            }
        }
    }
}";
        const string generator = @"using System.Collections.Generic;

namespace FibonacciTest
{
    public static class FibonacciGenerator
    {
        public  static IEnumerable<int> Fibonacci()
        {
            int current = 1, next = 1;
            while (true)
            {
                yield return current;
                next = current + (current = next);
            }
        }
    }
}";
        #endregion

        var server = GetCodeRunner();

        var workspace = new Workspace(workspaceType: "console", buffers: new[]
        {
            new Buffer("Program.cs", program, 0),
            new Buffer("generators/FibonacciGenerator.cs", generator, 0)
        });

        var result = await server.RunAsync(new WorkspaceRequest(workspace, BufferId.Parse("Program.cs")));

        result.Succeeded.Should().BeTrue();
        result.Output.Count.Should().Be(21);
        result.Output.Should().BeEquivalentTo("1", "1", "2", "3", "5", "8", "13", "21", "34", "55", "89", "144", "233", "377", "610", "987", "1597", "2584", "4181", "6765", "");
    }

    [Fact]
    public async Task Compile_response_with_multi_buffer_using_relative_paths_workspace()
    {
        #region bufferSources

        const string program = @"using System;
using System.Linq;

namespace FibonacciTest
{
    public class Program
    {
        public static void Main()
        {
            foreach (var i in FibonacciGenerator.Fibonacci().Take(20))
            {
                Console.WriteLine(i);
            }
        }
    }
}";
        const string generator = @"using System.Collections.Generic;

namespace FibonacciTest
{
    public static class FibonacciGenerator
    {
        public  static IEnumerable<int> Fibonacci()
        {
            int current = 1, next = 1;
            while (true)
            {
                yield return current;
                next = current + (current = next);
            }
        }
    }
}";
        #endregion

        var server = GetCodeCompiler();

        var workspace = new Workspace(workspaceType: "console", buffers: new[]
        {
            new Buffer("Program.cs", program, 0),
            new Buffer("generators/FibonacciGenerator.cs", generator, 0)
        });

        var result = await server.CompileAsync(new WorkspaceRequest(workspace, BufferId.Parse("Program.cs")));

        result.Succeeded.Should().BeTrue();
    }

    [Fact(Skip = "Needs moved onto Package2")]
    public async Task Compile_fails_when_instrumentation_enabled_and_there_is_an_error()
    {
        var server = GetCodeCompiler();
        var workspace = new Workspace(
            workspaceType: "console",
            files: new[] { new ProjectFileContent("Program.cs", SourceCodeProvider.ConsoleProgramSingleRegion) },
            buffers: new[] { new Buffer("Program.cs", @"Console.WriteLine(banana);", 0), },
            includeInstrumentation: true);

        var result = await server.CompileAsync(new WorkspaceRequest(workspace));

        result.Should().BeEquivalentTo(new
        {
            Succeeded = false,
            Output = new[] { "(1,19): error CS0103: The name \'banana\' does not exist in the current context" },
            Exception = (string)null, // we already display the error in Output
        }, config => config.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Can_compile_c_sharp_8_features()
    {
        var server = GetCodeRunner();

        var workspace = Workspace.FromSource(@"
using System;

public static class Hello
{
    public static void Main()
    {
        var i1 = 3;  // number 3 from beginning
        var i2 = ^4; // number 4 from end
        var a = new[]{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        Console.WriteLine($""{a[i1]}, {a[i2]}"");
    }
}
", workspaceType: "console");

        var result = await server.RunAsync(new WorkspaceRequest(workspace));

        result.Output.ShouldMatch(result.Succeeded
            ? "3, 6"
            : "*The feature 'index operator' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.");
    }
    
    protected override ILanguageService GetLanguageService() => new RoslynWorkspaceServer(PrebuildFinder.Create(() => Prebuild.GetOrCreateConsolePrebuildAsync(false)));

    protected override ICodeCompiler GetCodeCompiler() => new RoslynWorkspaceServer(PrebuildFinder.Create(() => Prebuild.GetOrCreateConsolePrebuildAsync(false)));

    protected override ICodeRunner GetCodeRunner() => new RoslynWorkspaceServer(PrebuildFinder.Create(() => Prebuild.GetOrCreateConsolePrebuildAsync(false)));
}
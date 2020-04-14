﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class DefaultRazorFormattingServiceTest : FormattingTestBase
    {
        [Fact]
        public async Task FormatsCodeBlockDirective()
        {
            await RunFormattingTestAsync(
input: @"
|@code {
 public class Foo{}
        public interface Bar {
}
}|
",
expected: @"
@code {
    public class Foo { }
    public interface Bar
    {
    }
}
");
        }

        [Fact]
        public async Task DoesNotFormat_NonCodeBlockDirectives()
        {
            await RunFormattingTestAsync(
input: @"
|@{
var x = ""foo"";
}
<div>
        </div>|
",
expected: @"
@{
var x = ""foo"";
}
<div>
        </div>
");
        }

        [Fact]
        public async Task DoesNotFormat_CodeBlockDirectiveWithMarkup()
        {
            await RunFormattingTestAsync(
input: @"
|@functions {
 public class Foo{
void Method() { <div></div> }
}
}|
",
expected: @"
@functions {
 public class Foo{
void Method() { <div></div> }
}
}
");
        }

        [Fact]
        public async Task DoesNotFormat_CodeBlockDirectiveWithImplicitExpressions()
        {
            await RunFormattingTestAsync(
input: @"
|@code {
 public class Foo{
void Method() { @DateTime.Now }
}
}|
",
expected: @"
@code {
 public class Foo{
void Method() { @DateTime.Now }
}
}
");
        }

        [Fact]
        public async Task DoesNotFormat_CodeBlockDirectiveWithExplicitExpressions()
        {
            await RunFormattingTestAsync(
input: @"
|@functions {
 public class Foo{
void Method() { @(DateTime.Now) }
}
}|
",
expected: @"
@functions {
 public class Foo{
void Method() { @(DateTime.Now) }
}
}
",
fileKind: FileKinds.Legacy);
        }

        [Fact]
        public async Task DoesNotFormat_CodeBlockDirectiveWithRazorComments()
        {
            await RunFormattingTestAsync(
input: @"
|@functions {
 public class Foo{
@* This is a Razor Comment *@
void Method() {  }
}
}|
",
expected: @"
@functions {
 public class Foo{
@* This is a Razor Comment *@
void Method() {  }
}
}
");
        }

        [Fact]
        public async Task DoesNotFormat_CodeBlockDirectiveWithRazorStatements()
        {
            await RunFormattingTestAsync(
input: @"
|@functions {
 public class Foo{
@* This is a Razor Comment *@
void Method() { @if (true) {} }
}
}|
",
expected: @"
@functions {
 public class Foo{
@* This is a Razor Comment *@
void Method() { @if (true) {} }
}
}
");
        }

        [Fact]
        public async Task OnlyFormatsWithinRange()
        {
            await RunFormattingTestAsync(
input: @"
@functions {
 public class Foo{}
        |public interface Bar {
}|
}
",
expected: @"
@functions {
 public class Foo{}
    public interface Bar
    {
    }
}
");
        }

        [Fact]
        public async Task MultipleCodeBlockDirectives()
        {
            await RunFormattingTestAsync(
input: @"
|@functions {
 public class Foo{}
        public interface Bar {
}
}
Hello World
@functions {
      public class Baz    {
          void Method ( )
          { }
          }
}|
",
expected: @"
@functions {
    public class Foo { }
    public interface Bar
    {
    }
}
Hello World
@functions {
    public class Baz
    {
        void Method()
        { }
    }
}
",
fileKind: FileKinds.Legacy);
        }

        [Fact]
        public async Task MultipleCodeBlockDirectives2()
        {
            await RunFormattingTestAsync(
input: @"|
Hello World
@code {
public class HelloWorld
{
}
}

@functions{
    
 public class Bar {}
}
|",
expected: @"
Hello World
@code {
    public class HelloWorld
    {
    }
}

@functions{
    
    public class Bar { }
}
");
        }

        [Fact]
        public async Task CodeOnTheSameLineAsCodeBlockDirectiveStart()
        {
            await RunFormattingTestAsync(
input: @"
|@functions {public class Foo{
}
}|
",
expected: @"
@functions {
    public class Foo
    {
    }
}
");
        }

        [Fact]
        public async Task CodeOnTheSameLineAsCodeBlockDirectiveEnd()
        {
            await RunFormattingTestAsync(
input: @"
|@functions {
public class Foo{
}}|
",
expected: @"
@functions {
    public class Foo
    {
    }
}
");
        }

        [Fact]
        public async Task SingleLineCodeBlockDirective()
        {
            await RunFormattingTestAsync(
input: @"
|@functions {public class Foo{}}|
",
expected: @"
@functions {
    public class Foo { }
}
");
        }

        [Fact]
        public async Task IndentsCodeBlockDirectiveStart()
        {
            await RunFormattingTestAsync(
input: @"|
Hello World
     @functions {public class Foo{}
}|
",
expected: @"
Hello World
@functions {
    public class Foo { }
}
");
        }

        [Fact]
        public async Task IndentsCodeBlockDirectiveEnd()
        {
            await RunFormattingTestAsync(
input: @"|
 @functions {
public class Foo{}
     }|
",
expected: @"
@functions {
    public class Foo { }
}
");
        }

        [Fact]
        public async Task ComplexCodeBlockDirective()
        {
            await RunFormattingTestAsync(
input: @"
@using System.Buffers
|@functions{
     public class Foo
            {
                public Foo()
                {
                    var arr = new string[ ] {
""One"", ""two"",
""three""
                    };
                }
public int MyProperty { get
{
return 0 ;
} set {} }

void Method(){

}
                    }
}|
",
expected: @"
@using System.Buffers
@functions{
    public class Foo
    {
        public Foo()
        {
            var arr = new string[] {
""One"", ""two"",
""three""
                };
        }
        public int MyProperty
        {
            get
            {
                return 0;
            }
            set { }
        }

        void Method()
        {

        }
    }
}
");
        }

        [Fact]
        public async Task CodeBlockDirective_UseTabs()
        {
            await RunFormattingTestAsync(
input: @"
|@code {
 public class Foo{}
        void Method(  ) {
}
}|
",
expected: @"
@code {
	public class Foo { }
	void Method()
	{
	}
}
",
insertSpaces: false);
        }

        [Fact]
        public async Task CodeBlockDirective_UseTabsWithTabSize8()
        {
            await RunFormattingTestAsync(
input: @"
|@code {
 public class Foo{}
        void Method(  ) {
}
}|
",
expected: @"
@code {
	public class Foo { }
	void Method()
	{
	}
}
",
tabSize: 8,
insertSpaces: false);
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/18996")]
        public async Task CodeBlockDirective_WithTabSize3()
        {
            await RunFormattingTestAsync(
input: @"
|@code {
 public class Foo{}
        void Method(  ) {
}
}|
",
expected: @"
@code {
   public class Foo { }
   void Method()
   {
   }
}
",
tabSize: 3);
        }

        [Fact]
        public async Task CodeBlockDirective_WithTabSize8()
        {
            await RunFormattingTestAsync(
input: @"
|@code {
 public class Foo{}
        void Method(  ) {
}
}|
",
expected: @"
@code {
        public class Foo { }
        void Method()
        {
        }
}
",
tabSize: 8);
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/18996")]
        public async Task CodeBlockDirective_WithTabSize12()
        {
            await RunFormattingTestAsync(
input: @"
|@code {
 public class Foo{}
        void Method(  ) {
}
}|
",
expected: @"
@code {
            public class Foo { }
            void Method()
            {
            }
}
",
tabSize: 12);
        }

        [Fact]
        public async Task OnTypeCloseAngle_ClosesTextTag()
        {
            await RunFormatOnTypeTestAsync(
input: @"
@{
    <text|
}
",
expected: $@"
@{{
    <text>{LanguageServerConstants.CursorPlaceholderString}</text>
}}
",
character: ">");
        }

        [Fact]
        public async Task OnTypeCloseAngle_ClosesTextTag_DoesNotReturnPlaceholder()
        {
            await RunFormatOnTypeTestAsync(
input: @"
@{
    <text|
}
",
expected: @"
@{
    <text></text>
}
",
character: ">", expectCursorPlaceholder: false);
        }

        [Fact]
        public async Task OnTypeCloseAngle_OutsideRazorBlock_DoesNotCloseTextTag()
        {
            await RunFormatOnTypeTestAsync(
input: @"
    <text|
",
expected: $@"
    <text>
",
character: ">");
        }

        [Fact]
        public async Task OnTypeStar_ClosesRazorComment()
        {
            await RunFormatOnTypeTestAsync(
input: @"
@|
",
expected: $@"
@* {LanguageServerConstants.CursorPlaceholderString} *@
",
character: "*");
        }

        [Fact]
        public async Task OnTypeStar_InsideRazorComment_Noops()
        {
            await RunFormatOnTypeTestAsync(
input: @"
@* @| *@
",
expected: $@"
@* @* *@
",
character: "*");
        }

        [Fact]
        public async Task OnTypeStar_EndRazorComment_Noops()
        {
            await RunFormatOnTypeTestAsync(
input: @"
@* Hello |@
",
expected: $@"
@* Hello *@
",
character: "*");
        }

        [Fact]
        public async Task OnTypeStar_BeforeText_ClosesRazorComment()
        {
            await RunFormatOnTypeTestAsync(
input: @"
@| Hello
",
expected: $@"
@* {LanguageServerConstants.CursorPlaceholderString} *@ Hello
",
character: "*");
        }
    }
}

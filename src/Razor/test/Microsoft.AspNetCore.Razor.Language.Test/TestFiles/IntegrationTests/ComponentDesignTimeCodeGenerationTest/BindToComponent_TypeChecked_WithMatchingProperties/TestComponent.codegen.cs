// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    public class TestComponent : Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 219
        private void __RazorDirectiveTokenHelpers__() {
        }
        #pragma warning restore 219
        #pragma warning disable 0414
        private static System.Object __o = null;
        #pragma warning restore 0414
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.RenderTree.RenderTreeBuilder builder)
        {
            base.BuildRenderTree(builder);
            __o = Microsoft.AspNetCore.Components.RuntimeHelpers.TypeCheck<System.Int32>(Microsoft.AspNetCore.Components.BindMethods.GetValue(
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                         ParentValue

#line default
#line hidden
            ));
            __o = new System.Action<System.Int32>(
            __value => ParentValue = __value);
            builder.AddAttribute(-1, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)((builder2) => {
            }
            ));
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
__o = typeof(MyComponent);

#line default
#line hidden
        }
        #pragma warning restore 1998
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
            
    public string ParentValue { get; set; } = "42";

#line default
#line hidden
    }
}
#pragma warning restore 1591

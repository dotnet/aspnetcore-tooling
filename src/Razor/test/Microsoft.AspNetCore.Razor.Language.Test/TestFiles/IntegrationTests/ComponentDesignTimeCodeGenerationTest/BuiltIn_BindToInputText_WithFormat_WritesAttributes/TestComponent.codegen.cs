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
        ((System.Action)(() => {
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
global::System.Object __typeHelper = "*, TestAssembly";

#line default
#line hidden
        }
        ))();
        }
        #pragma warning restore 219
        #pragma warning disable 0414
        private static System.Object __o = null;
        #pragma warning restore 0414
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.RenderTree.RenderTreeBuilder builder)
        {
            base.BuildRenderTree(builder);
            __o = Microsoft.AspNetCore.Components.BindMethods.GetValue(CurrentDate, "MM/dd/yyyy");
            __o = Microsoft.AspNetCore.Components.EventCallback.Factory.CreateBinder(this, __value => CurrentDate = __value, CurrentDate, "MM/dd/yyyy");
        }
        #pragma warning restore 1998
#line 3 "x:\dir\subdir\Test\TestComponent.cshtml"
            
    public DateTime CurrentDate { get; set; } = new DateTime(2018, 1, 1);

#line default
#line hidden
    }
}
#pragma warning restore 1591

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
    using Microsoft.AspNetCore.Components.RenderTree;
    public class TestComponent : Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.RenderTree.RenderTreeBuilder builder)
        {
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
  
    void RenderChildComponent()
    {

#line default
#line hidden
#nullable disable
            builder.AddContent(0, "        ");
            builder.OpenComponent<Test.MyComponent>(1);
            builder.CloseComponent();
            builder.AddMarkupContent(2, "\r\n");
#nullable restore
#line 6 "x:\dir\subdir\Test\TestComponent.cshtml"
    }

#line default
#line hidden
#nullable disable
            builder.AddMarkupContent(3, "\r\n");
#nullable restore
#line 9 "x:\dir\subdir\Test\TestComponent.cshtml"
   RenderChildComponent(); 

#line default
#line hidden
#nullable disable
        }
        #pragma warning restore 1998
    }
}
#pragma warning restore 1591

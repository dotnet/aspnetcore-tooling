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
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.RenderTree.RenderTreeBuilder builder)
        {
            builder.AddContent(0, 
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
  RenderPerson((person) => 

#line default
#line hidden
#nullable disable
            (builder2) => {
                builder2.OpenElement(1, "div");
                builder2.AddContent(2, 
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                                  person.Name

#line default
#line hidden
#nullable disable
                );
                builder2.CloseElement();
            }
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                                                   )

#line default
#line hidden
#nullable disable
            );
        }
        #pragma warning restore 1998
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
            
    class Person
    {
        public string Name { get; set; }
    }

    object RenderPerson(RenderFragment<Person> p) => null;

#line default
#line hidden
#nullable disable
    }
}
#pragma warning restore 1591

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components
{
    internal class ComponentBindLoweringPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
    {
        // Run after event handler pass
        public override int Order => 100;

        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            if (!IsComponentDocument(documentNode))
            {
                return;
            }

            var @namespace = documentNode.FindPrimaryNamespace();
            var @class = documentNode.FindPrimaryClass();
            if (@namespace == null || @class == null)
            {
                // Nothing to do, bail. We can't function without the standard structure.
                return;
            }

            // For each bind *usage* we need to rewrite the tag helper node to map to basic constructs.
            var references = documentNode.FindDescendantReferences<TagHelperPropertyIntermediateNode>();

            var parents = new HashSet<IntermediateNode>();
            for (var i = 0; i < references.Count; i++)
            {
                parents.Add(references[i].Parent);
            }

            foreach (var parent in parents)
            {
                ProcessDuplicates(parent);
            }

            var bindEntries = new Dictionary<string, BindEntry>();
            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];
                var node = (TagHelperPropertyIntermediateNode)reference.Node;

                if (!reference.Parent.Children.Contains(node))
                {
                    // This node was removed as a duplicate, skip it.
                    continue;
                }

                if (node.TagHelper.IsBindTagHelper() && node.AttributeName.StartsWith("bind") && !node.IsParameterMatch)
                {
                    bindEntries[node.AttributeName] = new BindEntry(reference);
                }
            }

            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];
                var node = (TagHelperPropertyIntermediateNode)reference.Node;

                if (!reference.Parent.Children.Contains(node))
                {
                    // This node was removed as a duplicate, skip it.
                    continue;
                }

                if (node.TagHelper.IsBindTagHelper() && node.AttributeName.StartsWith("bind") && node.IsParameterMatch)
                {
                    var originalAttributeName = node.AttributeName.Split(':')[0];
                    if (!bindEntries.ContainsKey(originalAttributeName))
                    {
                        // There is no corresponding bind node. Add a diagnostic and move on.
                        reference.Parent.Diagnostics.Add(ComponentDiagnosticFactory.CreateBindAttributeParameter_MissingBind(
                            node.Source,
                            node.AttributeName));
                    }
                    else if (node.BoundAttributeParameter.Name == "event")
                    {
                        bindEntries[originalAttributeName].BindEventNode = node;
                    }
                    else if (node.BoundAttributeParameter.Name == "format")
                    {
                        bindEntries[originalAttributeName].BindFormatNode = node;
                    }
                    else
                    {
                        // Unsupported bind attribute parameter.
                    }

                    // We've extracted what we need from the parameterized bind node. Remove it.
                    reference.Remove();
                }
            }

            foreach (var entry in bindEntries)
            {
                var reference = entry.Value.BindNodeReference;
                // Workaround for https://github.com/aspnet/Blazor/issues/703
                var rewritten = RewriteUsage(reference.Parent, entry.Value);
                reference.Remove();

                for (var j = 0; j < rewritten.Length; j++)
                {
                    reference.Parent.Children.Add(rewritten[j]);
                }
            }
        }

        private void ProcessDuplicates(IntermediateNode node)
        {
            // Reverse order because we will remove nodes.
            //
            // Each 'property' node could be duplicated if there are multiple tag helpers that match that
            // particular attribute. This is common in our approach, which relies on 'fallback' tag helpers
            // that overlap with more specific ones.
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                // For each usage of the general 'fallback' bind tag helper, it could duplicate
                // the usage of a more specific one. Look for duplicates and remove the fallback.
                var attribute = node.Children[i] as TagHelperPropertyIntermediateNode;
                if (attribute != null &&
                    attribute.TagHelper != null &&
                    attribute.TagHelper.IsFallbackBindTagHelper())
                {
                    for (var j = 0; j < node.Children.Count; j++)
                    {
                        var duplicate = node.Children[j] as TagHelperPropertyIntermediateNode;
                        if (duplicate != null &&
                            duplicate.TagHelper != null &&
                            duplicate.TagHelper.IsBindTagHelper() &&
                            duplicate.AttributeName == attribute.AttributeName &&
                            !object.ReferenceEquals(attribute, duplicate))
                        {
                            // Found a duplicate - remove the 'fallback' in favor of the
                            // more specific tag helper.
                            node.Children.RemoveAt(i);
                            break;
                        }
                    }
                }

                // Also treat the general <input bind="..." /> as a 'fallback' for that case and remove it.
                // This is a workaround for a limitation where you can't write a tag helper that binds only
                // when a specific attribute is **not** present.
                if (attribute != null &&
                    attribute.TagHelper != null &&
                    attribute.TagHelper.IsInputElementFallbackBindTagHelper())
                {
                    for (var j = 0; j < node.Children.Count; j++)
                    {
                        var duplicate = node.Children[j] as TagHelperPropertyIntermediateNode;
                        if (duplicate != null &&
                            duplicate.TagHelper != null &&
                            duplicate.TagHelper.IsInputElementBindTagHelper() &&
                            duplicate.AttributeName == attribute.AttributeName &&
                            !object.ReferenceEquals(attribute, duplicate))
                        {
                            // Found a duplicate - remove the 'fallback' input tag helper in favor of the
                            // more specific tag helper.
                            node.Children.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            // If we still have duplicates at this point then they are genuine conflicts.
            var duplicates = node.Children
                .OfType<TagHelperPropertyIntermediateNode>()
                .GroupBy(p => p.AttributeName)
                .Where(g => g.Count() > 1);

            foreach (var duplicate in duplicates)
            {
                node.Diagnostics.Add(ComponentDiagnosticFactory.CreateBindAttribute_Duplicates(
                    node.Source,
                    duplicate.Key,
                    duplicate.ToArray()));
                foreach (var property in duplicate)
                {
                    node.Children.Remove(property);
                }
            }
        }

        private IntermediateNode[] RewriteUsage(IntermediateNode parent, BindEntry bindEntry)
        {
            // Bind works similarly to a macro, it always expands to code that the user could have written.
            //
            // For the nodes that are related to the bind-attribute rewrite them to look like a set of
            // 'normal' HTML attributes similar to the following transformation.
            //
            // Input:   <MyComponent bind-Value="@currentCount" />
            // Output:  <MyComponent Value ="...<get the value>..." ValueChanged ="... <set the value>..." ValueExpression ="() => ...<get the value>..." />
            //
            // This means that the expression that appears inside of 'bind' must be an LValue or else
            // there will be errors. In general the errors that come from C# in this case are good enough
            // to understand the problem.
            //
            // We also support and encourage the use of EventCallback<> with bind. So in the above example
            // the ValueChanged property could be an Action<> or an EventCallback<>.
            //
            // The BindMethods calls are required with Action<> because to give us a good experience. They
            // use overloading to ensure that can get an Action<object> that will convert and set an arbitrary
            // value. We have a similar set of APIs to use with EventCallback<>.
            //
            // We also assume that the element will be treated as a component for now because
            // multiple passes handle 'special' tag helpers. We have another pass that translates
            // a tag helper node back into 'regular' element when it doesn't have an associated component
            var node = bindEntry.BindNode;
            if (!TryComputeAttributeNames(
                parent,
                bindEntry,
                out var valueAttributeName,
                out var changeAttributeName,
                out var expressionAttributeName,
                out var valueAttribute,
                out var changeAttribute,
                out var expressionAttribute))
            {
                // Skip anything we can't understand. It's important that we don't crash, that will bring down
                // the build.
                node.Diagnostics.Add(ComponentDiagnosticFactory.CreateBindAttribute_InvalidSyntax(
                    node.Source, 
                    node.AttributeName));
                return new[] { node };
            }

            var original = GetAttributeContent(node);
            if (string.IsNullOrEmpty(original.Content))
            {
                // This can happen in error cases, the parser will already have flagged this
                // as an error, so ignore it.
                return new[] { node };
            }

            // Look for a matching format node. If we find one then we need to pass the format into the
            // two nodes we generate.
            IntermediateToken format = null;
            if (bindEntry.BindFormatNode != null)
            {
                format = GetAttributeContent(bindEntry.BindFormatNode);
            }

            var valueExpressionTokens = new List<IntermediateToken>();
            var changeExpressionTokens = new List<IntermediateToken>();
            if (changeAttribute != null && changeAttribute.IsDelegateProperty())
            {
                RewriteNodesForDelegateBind(
                    original,
                    format,
                    valueAttribute,
                    changeAttribute,
                    valueExpressionTokens, 
                    changeExpressionTokens);
            }
            else
            {
                RewriteNodesForEventCallbackBind(
                    original,
                    format,
                    valueAttribute,
                    changeAttribute,
                    valueExpressionTokens,
                    changeExpressionTokens);
            }

            if (parent is MarkupElementIntermediateNode)
            {
                var valueNode = new HtmlAttributeIntermediateNode()
                {
                    AttributeName = valueAttributeName,
                    Source = node.Source,

                    Prefix = valueAttributeName + "=\"",
                    Suffix = "\"",
                };

                for (var i = 0; i < node.Diagnostics.Count; i++)
                {
                    valueNode.Diagnostics.Add(node.Diagnostics[i]);
                }

                valueNode.Children.Add(new CSharpExpressionAttributeValueIntermediateNode());
                for (var i = 0; i < valueExpressionTokens.Count; i++)
                {
                    valueNode.Children[0].Children.Add(valueExpressionTokens[i]);
                }

                var changeNode = new HtmlAttributeIntermediateNode()
                {
                    AttributeName = changeAttributeName,
                    Source = node.Source,

                    Prefix = changeAttributeName + "=\"",
                    Suffix = "\"",
                };

                changeNode.Children.Add(new CSharpExpressionAttributeValueIntermediateNode());
                for (var i = 0; i < changeExpressionTokens.Count; i++)
                {
                    changeNode.Children[0].Children.Add(changeExpressionTokens[i]);
                }

                return  new[] { valueNode, changeNode };
            }
            else
            {
                var valueNode = new ComponentAttributeIntermediateNode(node)
                {
                    AttributeName = valueAttributeName,
                    BoundAttribute = valueAttribute, // Might be null if it doesn't match a component attribute
                    PropertyName = valueAttribute?.GetPropertyName(),
                    TagHelper = valueAttribute == null ? null : node.TagHelper,
                    TypeName = valueAttribute?.IsWeaklyTyped() == false ? valueAttribute.TypeName : null,
                };

                valueNode.Children.Clear();
                valueNode.Children.Add(new CSharpExpressionIntermediateNode());
                for (var i = 0; i < valueExpressionTokens.Count; i++)
                {
                    valueNode.Children[0].Children.Add(valueExpressionTokens[i]);
                }

                var changeNode = new ComponentAttributeIntermediateNode(node)
                {
                    AttributeName = changeAttributeName,
                    BoundAttribute = changeAttribute, // Might be null if it doesn't match a component attribute
                    PropertyName = changeAttribute?.GetPropertyName(),
                    TagHelper = changeAttribute == null ? null : node.TagHelper,
                    TypeName = changeAttribute?.IsWeaklyTyped() == false ? changeAttribute.TypeName : null,
                };

                changeNode.Children.Clear();
                changeNode.Children.Add(new CSharpExpressionIntermediateNode());
                for (var i = 0; i < changeExpressionTokens.Count; i++)
                {
                    changeNode.Children[0].Children.Add(changeExpressionTokens[i]);
                }

                // Finally, also emit a node for the "Expression" attribute, but only if the target
                // component is defined to accept one
                ComponentAttributeIntermediateNode expressionNode = null;
                if (expressionAttribute != null)
                {
                    expressionNode = new ComponentAttributeIntermediateNode(node)
                    {
                        AttributeName = expressionAttributeName,
                        BoundAttribute = expressionAttribute,
                        PropertyName = expressionAttribute.GetPropertyName(),
                        TagHelper = node.TagHelper,
                        TypeName = expressionAttribute.IsWeaklyTyped() ? null : expressionAttribute.TypeName,
                    };

                    expressionNode.Children.Clear();
                    expressionNode.Children.Add(new CSharpExpressionIntermediateNode());
                    expressionNode.Children[0].Children.Add(new IntermediateToken()
                    {
                        Content = $"() => {original.Content}",
                        Kind = TokenKind.CSharp
                    });
                }

                return expressionNode == null
                    ? new[] { valueNode, changeNode }
                    : new[] { valueNode, changeNode, expressionNode };
            }
        }

        private bool TryParseBindAttribute(
            BindEntry bindEntry,
            out string valueAttributeName,
            out string changeAttributeName)
        {
            var attributeName = bindEntry.BindNode.AttributeName;
            valueAttributeName = null;
            changeAttributeName = null;

            if (!attributeName.StartsWith("bind"))
            {
                return false;
            }

            if (bindEntry.BindEventNode != null)
            {
                changeAttributeName = GetAttributeContent(bindEntry.BindEventNode)?.Content?.Trim('"');
            }

            if (attributeName == "bind")
            {
                return true;
            }

            var segments = attributeName.Split('-');
            for (var i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrEmpty(segments[i]))
                {
                    return false;
                }
            }

            switch (segments.Length)
            {
                case 2:
                    valueAttributeName = segments[1];
                    return true;

                case 3:
                    valueAttributeName = segments[1];
                    bindEntry.BindNode.Diagnostics.Add(ComponentDiagnosticFactory.CreateBindAttribute_UnsupportedFormat(bindEntry.BindNode.Source));
                    return true;

                default:
                    return false;
            }
        }

        // Attempts to compute the attribute names that should be used for an instance of 'bind'.
        private bool TryComputeAttributeNames(
            IntermediateNode parent,
            BindEntry bindEntry,
            out string valueAttributeName,
            out string changeAttributeName,
            out string expressionAttributeName,
            out BoundAttributeDescriptor valueAttribute,
            out BoundAttributeDescriptor changeAttribute,
            out BoundAttributeDescriptor expressionAttribute)
        {
            valueAttribute = null;
            changeAttribute = null;
            expressionAttribute = null;
            expressionAttributeName = null;

            // Even though some of our 'bind' tag helpers specify the attribute names, they
            // should still satisfy one of the valid syntaxes.
            if (!TryParseBindAttribute(bindEntry, out valueAttributeName, out changeAttributeName))
            {
                return false;
            }

            // The tag helper specifies attribute names, they should win.
            //
            // This handles cases like <input type="text" bind="@Foo" /> where the tag helper is 
            // generated to match a specific tag and has metadata that identify the attributes.
            //
            // We expect 1 bind tag helper per-node.
            var node = bindEntry.BindNode;
            var attributeName = node.AttributeName;
            valueAttributeName = node.TagHelper.GetValueAttributeName() ?? valueAttributeName;
            changeAttributeName = node.TagHelper.GetChangeAttributeName() ?? changeAttributeName;
            expressionAttributeName = node.TagHelper.GetExpressionAttributeName() ?? expressionAttributeName;

            // We expect 0-1 components per-node.
            var componentTagHelper = (parent as ComponentIntermediateNode)?.Component;
            if (componentTagHelper == null)
            {
                // If it's not a component node then there isn't too much else to figure out.
                return attributeName != null && changeAttributeName != null;
            }

            // If this is a component, we need an attribute name for the value.
            if (attributeName == null)
            {
                return false;
            }

            // If this is a component, then we can infer '<PropertyName>Changed' as the name
            // of the change event.
            if (changeAttributeName == null)
            {
                changeAttributeName = valueAttributeName + "Changed";
            }

            // Likewise for the expression attribute
            if (expressionAttributeName == null)
            {
                expressionAttributeName = valueAttributeName + "Expression";
            }

            for (var i = 0; i < componentTagHelper.BoundAttributes.Count; i++)
            {
                var attribute = componentTagHelper.BoundAttributes[i];

                if (string.Equals(valueAttributeName, attribute.Name))
                {
                    valueAttribute = attribute;
                }

                if (string.Equals(changeAttributeName, attribute.Name))
                {
                    changeAttribute = attribute;
                }

                if (string.Equals(expressionAttributeName, attribute.Name))
                {
                    expressionAttribute = attribute;
                }
            }

            return true;
        }

        private bool TryGetFormatNode(
            IntermediateNode node,
            TagHelperPropertyIntermediateNode attributeNode,
            string valueAttributeName,
            out TagHelperPropertyIntermediateNode formatNode)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i] as TagHelperPropertyIntermediateNode;
                if (child != null &&
                    child.TagHelper != null &&
                    child.TagHelper == attributeNode.TagHelper &&
                    child.AttributeName == "format-" + valueAttributeName)
                {
                    formatNode = child;
                    return true;
                }
            }

            formatNode = null;
            return false;
        }

        private void RewriteNodesForDelegateBind(
            IntermediateToken original,
            IntermediateToken format,
            BoundAttributeDescriptor valueAttribute,
            BoundAttributeDescriptor changeAttribute,
            List<IntermediateToken> valueExpressionTokens, 
            List<IntermediateToken> changeExpressionTokens)
        {
            // Now rewrite the content of the value node to look like:
            //
            // BindMethods.GetValue(<code>) OR
            // BindMethods.GetValue(<code>, <format>)
            valueExpressionTokens.Add(new IntermediateToken()
            {
                Content = $"{ComponentsApi.BindMethods.GetValue}(",
                Kind = TokenKind.CSharp
            });
            valueExpressionTokens.Add(original);
            if (!string.IsNullOrEmpty(format?.Content))
            {
                valueExpressionTokens.Add(new IntermediateToken()
                {
                    Content = ", ",
                    Kind = TokenKind.CSharp,
                });
                valueExpressionTokens.Add(format);
            }
            valueExpressionTokens.Add(new IntermediateToken()
            {
                Content = ")",
                Kind = TokenKind.CSharp,
            });

            // Now rewrite the content of the change-handler node. There are two cases we care about
            // here. If it's a component attribute, then don't use the 'BindMethods' wrapper. We expect
            // component attributes to always 'match' on type.
            //
            // __value => <code> = __value
            //
            // For general DOM attributes, we need to be able to create a delegate that accepts UIEventArgs
            // so we use BindMethods.SetValueHandler
            //
            // BindMethods.SetValueHandler(__value => <code> = __value, <code>) OR
            // BindMethods.SetValueHandler(__value => <code> = __value, <code>, <format>)
            //
            // Note that the linemappings here are applied to the value attribute, not the change attribute.

            string changeExpressionContent;
            if (changeAttribute == null && format == null)
            {
                // DOM
                changeExpressionContent = $"{ComponentsApi.BindMethods.SetValueHandler}(__value => {original.Content} = __value, {original.Content})";
            }
            else if (changeAttribute == null && format != null)
            {
                // DOM + format
                changeExpressionContent = $"{ComponentsApi.BindMethods.SetValueHandler}(__value => {original.Content} = __value, {original.Content}, {format.Content})";
            }
            else
            {
                // Component
                changeExpressionContent = $"__value => {original.Content} = __value";
            }
            changeExpressionTokens.Add(new IntermediateToken()
            {
                Content = changeExpressionContent,
                Kind = TokenKind.CSharp
            });              
        }

        private void RewriteNodesForEventCallbackBind(
            IntermediateToken original,
            IntermediateToken format,
            BoundAttributeDescriptor valueAttribute,
            BoundAttributeDescriptor changeAttribute,
            List<IntermediateToken> valueExpressionTokens,
            List<IntermediateToken> changeExpressionTokens)
        {
            // Now rewrite the content of the value node to look like:
            //
            // BindMethods.GetValue(<code>) OR
            // BindMethods.GetValue(<code>, <format>)
            valueExpressionTokens.Add(new IntermediateToken()
            {
                Content = $"{ComponentsApi.BindMethods.GetValue}(",
                Kind = TokenKind.CSharp
            });
            valueExpressionTokens.Add(original);
            if (!string.IsNullOrEmpty(format?.Content))
            {
                valueExpressionTokens.Add(new IntermediateToken()
                {
                    Content = ", ",
                    Kind = TokenKind.CSharp,
                });
                valueExpressionTokens.Add(format);
            }
            valueExpressionTokens.Add(new IntermediateToken()
            {
                Content = ")",
                Kind = TokenKind.CSharp,
            });

            // Now rewrite the content of the change-handler node. There are two cases we care about
            // here. If it's a component attribute, then don't use the 'CreateBinder' wrapper. We expect
            // component attributes to always 'match' on type.
            //
            // The really tricky part of this is that we CANNOT write the type name of of the EventCallback we
            // intend to create. Doing so would really complicate the story for how we deal with generic types,
            // since the generic type lowering pass runs after this. To keep this simple we're relying on
            // the compiler to resolve overloads for us.
            //
            // EventCallbackFactory.CreateInferred(this, __value => <code> = __value, <code>)
            //
            // For general DOM attributes, we need to be able to create a delegate that accepts UIEventArgs
            // so we use 'CreateBinder'
            //
            // EventCallbackFactory.CreateBinder(this, __value => <code> = __value, <code>) OR
            // EventCallbackFactory.CreateBinder(this, __value => <code> = __value, <code>, <format>)
            //
            // Note that the linemappings here are applied to the value attribute, not the change attribute.

            string changeExpressionContent;
            if (changeAttribute == null && format == null)
            {
                // DOM
                changeExpressionContent = $"{ComponentsApi.EventCallback.FactoryAccessor}.{ComponentsApi.EventCallbackFactory.CreateBinderMethod}(this, __value => {original.Content} = __value, {original.Content})";
            }
            else if (changeAttribute == null && format != null)
            {
                // DOM + format
                changeExpressionContent = $"{ComponentsApi.EventCallback.FactoryAccessor}.{ComponentsApi.EventCallbackFactory.CreateBinderMethod}(this, __value => {original.Content} = __value, {original.Content}, {format.Content})";
            }
            else
            {
                // Component
                changeExpressionContent = $"{ComponentsApi.EventCallback.FactoryAccessor}.{ComponentsApi.EventCallbackFactory.CreateInferredMethod}(this, __value => {original.Content} = __value, {original.Content})";
            }
            changeExpressionTokens.Add(new IntermediateToken()
            {
                Content = changeExpressionContent,
                Kind = TokenKind.CSharp
            });
        }

        private static IntermediateToken GetAttributeContent(TagHelperPropertyIntermediateNode node)
        {
            var template = node.FindDescendantNodes<TemplateIntermediateNode>().FirstOrDefault();
            if (template != null)
            {
                // See comments in TemplateDiagnosticPass
                node.Diagnostics.Add(ComponentDiagnosticFactory.Create_TemplateInvalidLocation(template.Source));
                return new IntermediateToken() { Kind = TokenKind.CSharp, Content = string.Empty, };
            }

            if (node.Children[0] is HtmlContentIntermediateNode htmlContentNode)
            {
                // This case can be hit for a 'string' attribute. We want to turn it into
                // an expression.
                var content = "\"" + string.Join(string.Empty, htmlContentNode.Children.OfType<IntermediateToken>().Select(t => t.Content)) + "\"";
                return new IntermediateToken() { Kind = TokenKind.CSharp, Content = content };
            }
            else if (node.Children[0] is CSharpExpressionIntermediateNode cSharpNode)
            {
                // This case can be hit when the attribute has an explicit @ inside, which
                // 'escapes' any special sugar we provide for codegen.
                return GetToken(cSharpNode);
            }
            else
            {
                // This is the common case for 'mixed' content
                return GetToken(node);
            }

            IntermediateToken GetToken(IntermediateNode parent)
            {
                if (parent.Children.Count == 1 && parent.Children[0] is IntermediateToken token)
                {
                    return token;
                }

                // In error cases we won't have a single token, but we still want to generate the code.
                return new IntermediateToken()
                {
                    Kind = TokenKind.CSharp,
                    Content = string.Join(string.Empty, parent.Children.OfType<IntermediateToken>().Select(t => t.Content)),
                };
            }
        }

        private class BindEntry
        {
            public BindEntry(IntermediateNodeReference bindNodeReference)
            {
                BindNodeReference = bindNodeReference;
                BindNode = (TagHelperPropertyIntermediateNode)bindNodeReference.Node;
            }

            public IntermediateNodeReference BindNodeReference { get; }

            public TagHelperPropertyIntermediateNode BindNode { get; }

            public TagHelperPropertyIntermediateNode BindEventNode { get; set; }

            public TagHelperPropertyIntermediateNode BindFormatNode { get; set; }
        }
    }
}

﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Xunit;
using static Microsoft.AspNetCore.Razor.LanguageServer.Tooltip.VSLSPTagHelperTooltipFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    public class VSLSPTagHelperDescriptionFactoryTest
    {
        [Fact]
        public void CleanAndClassifySummaryContent_ReplacesSeeCrefs()
        {
            // Arrange
            var runs = new List<VSClassifiedTextRun>();
            var summary = "Accepts <see cref=\"T:System.Collections.List{System.String}\" />s";

            // Act
            CleanAndClassifySummaryContent(runs, summary);

            // Assert

            // Expected output:
            //     Accepts List<string>s
            Assert.Collection(runs, run => AssertExpectedClassification(run, "Accepts ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
        }

        [Fact]
        public void CleanSummaryContent_ReplacesSeeAlsoCrefs()
        {
            // Arrange
            var runs = new List<VSClassifiedTextRun>();
            var summary = "Accepts <seealso cref=\"T:System.Collections.List{System.String}\" />s";

            // Act
            CleanAndClassifySummaryContent(runs, summary);

            // Assert

            // Expected output:
            //     Accepts List<string>s
            Assert.Collection(runs, run => AssertExpectedClassification(run, "Accepts ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
        }

        [Fact]
        public void CleanSummaryContent_TrimsSurroundingWhitespace()
        {
            // Arrange
            var runs = new List<VSClassifiedTextRun>();
            var summary = @"
            Hello

    World

";

            // Act
            CleanAndClassifySummaryContent(runs, summary);

            // Assert

            // Expected output:
            //     Hello
            //
            //     World
            Assert.Collection(runs, run => AssertExpectedClassification(
                run, "Hello" + Environment.NewLine + Environment.NewLine + "World", VSPredefinedClassificationTypeNames.Text));
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_NoAssociatedTagHelperDescriptions_ReturnsFalse()
        {
            // Arrange
            var descriptionFactory = new VSLSPTagHelperTooltipFactory();
            var elementDescription = AggregateBoundElementDescription.Default;

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out var classifiedTextElement);

            // Assert
            Assert.False(result);
            Assert.Null(classifiedTextElement);
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Element_SingleAssociatedTagHelper_ReturnsTrue_NestedTypes()
        {
            // Arrange
            var descriptionFactory = new VSLSPTagHelperTooltipFactory();
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo(
                    "Microsoft.AspNetCore.SomeTagHelper",
                    "<summary>Uses <see cref=\"T:System.Collections.List{System.Collections.List{System.String}}\" />s</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out var classifiedTextElement);

            // Assert
            Assert.True(result);

            // Expected output:
            //     Microsoft.AspNetCore.SomeTagHelper
            //     Uses List<List<string>>s
            Assert.Collection(classifiedTextElement.Runs, run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Element_NamespaceContainsTypeName_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new VSLSPTagHelperTooltipFactory();
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo(
                    "Microsoft.AspNetCore.SomeTagHelper.SomeTagHelper",
                    "<summary>Uses <see cref=\"T:A.B.C{C.B}\" />s</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out var classifiedTextElement);

            // Assert
            Assert.True(result);

            // Expected output:
            //     Microsoft.AspNetCore.SomeTagHelper.SomeTagHelper
            //     Uses C<B>s
            Assert.Collection(classifiedTextElement.Runs, run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "C", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "B", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Element_MultipleAssociatedTagHelpers_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new VSLSPTagHelperTooltipFactory();
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.OtherTagHelper", "<summary>\nAlso uses <see cref=\"T:System.Collections.List{System.String}\" />s\n\r\n\r\r</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out var classifiedTextElement);

            // Assert
            Assert.True(result);

            // Expected output:
            //     Microsoft.AspNetCore.SomeTagHelper
            //     Uses List<string>s
            //
            //     Microsoft.AspNetCore.OtherTagHelper
            //     Also uses List<string>s
            Assert.Collection(classifiedTextElement.Runs, run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "OtherTagHelper", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Also uses ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_NoAssociatedAttributeDescriptions_ReturnsFalse()
        {
            // Arrange
            var descriptionFactory = new VSLSPTagHelperTooltipFactory();
            var elementDescription = AggregateBoundAttributeDescription.Default;

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out var classifiedTextElement);

            // Assert
            Assert.False(result);
            Assert.Null(classifiedTextElement);
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Attribute_SingleAssociatedAttribute_ReturnsTrue_NestedTypes()
        {
            // Arrange
            var descriptionFactory = new VSLSPTagHelperTooltipFactory();
            var associatedAttributeDescriptions = new[]
            {
                new BoundAttributeDescriptionInfo(
                    returnTypeName: "System.String",
                    typeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                    propertyName: "SomeProperty",
                    documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.Collections.List{System.String}}\" />s</summary>")
            };
            var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions);

            // Act
            var result = descriptionFactory.TryCreateTooltip(attributeDescription, out var classifiedTextElement);

            // Assert
            Assert.True(result);

            // Expected output:
            //     string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
            //     Uses List<List<string>>s
            Assert.Collection(classifiedTextElement.Runs, run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTagHelpers", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTypeName", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeProperty", VSPredefinedClassificationTypeNames.Identifier),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Attribute_MultipleAssociatedAttributes_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new VSLSPTagHelperTooltipFactory();
            var associatedAttributeDescriptions = new[]
            {
                new BoundAttributeDescriptionInfo(
                    returnTypeName: "System.String",
                    typeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                    propertyName: "SomeProperty",
                    documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>"),
                new BoundAttributeDescriptionInfo(
                    propertyName: "AnotherProperty",
                    typeName: "Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName",
                    returnTypeName: "System.Boolean?",
                    documentation: "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
            };
            var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions);

            // Act
            var result = descriptionFactory.TryCreateTooltip(attributeDescription, out var classifiedTextElement);

            // Assert
            Assert.True(result);

            // Expected output:
            //     string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
            //     Uses List<string>s
            //
            //     bool? Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName.AnotherProperty
            //     Uses List<string>s
            Assert.Collection(classifiedTextElement.Runs, run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTagHelpers", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTypeName", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeProperty", VSPredefinedClassificationTypeNames.Identifier),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "bool", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, "?", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "SomeTagHelpers", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AnotherTypeName", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "AnotherProperty", VSPredefinedClassificationTypeNames.Identifier),
                run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
                run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
                run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
                run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
                run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
                run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
        }

        private static void AssertExpectedClassification(VSClassifiedTextRun run, string expectedText, string expectedClassificationType)
        {
            Assert.Equal(expectedText, run.Text);
            Assert.Equal(expectedClassificationType, run.ClassificationTypeName);
        }
    }
}

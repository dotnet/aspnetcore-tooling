﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable

using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class SemanticRange : IComparable<SemanticRange>
    {
        public SemanticRange(int kind, Range range, int modifier, string? resultId)
        {
            Kind = kind;
            Modifier = modifier;
            Range = range;
            ResultId = resultId;
        }

        public Range Range { get; }

        public int Kind { get; }

        public int Modifier { get; }

        public string? ResultId { get; }

        public int CompareTo(SemanticRange other)
        {
            if (other is null)
            {
                return 1;
            }

            var startCompare = Range.Start.CompareTo(other.Range.Start);
            return startCompare != 0 ? startCompare : Range.End.CompareTo(other.Range.End);
        }

        public override string ToString()
        {
            return $"[Kind: {Kind}, Range: {Range}, ResultId: {ResultId}]";
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class TypeMustExistTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("CP002")]
        public void MissingPublicTypesInRightAreReported(string noWarn)
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
  public record MyRecord(string a, string b);
  public struct MyStruct { }
  public delegate void MyDelegate(object a);
  public enum MyEnum { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            ApiComparer differ = new();
            differ.NoWarn = noWarn;
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Second' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.Second"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyRecord' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.MyRecord"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyStruct' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.MyStruct"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyDelegate' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.MyDelegate"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyEnum' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.MyEnum"),
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public void MissingTypeFromTypeForwardIsReported()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string leftSyntax = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
namespace CompatTests
{
  public class First { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntaxWithReferences(leftSyntax, new[] { forwardedTypeSyntax }, includeDefaultReferences: true);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.ForwardedTestType' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.ForwardedTestType")
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public void TypeForwardExistsOnBoth()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string syntax = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
namespace CompatTests
{
  public class First { }
}
";
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, references, includeDefaultReferences: true);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, references, includeDefaultReferences: true);
            ApiComparer differ = new();
            Assert.Empty(differ.GetDifferences(new[] { left }, new[] { right }));
        }

        [Fact]
        public void NoDifferencesReportedWithNoWarn()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            ApiComparer differ = new();
            differ.NoWarn = DiagnosticIds.TypeMustExist;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            Assert.Empty(differ.GetDifferences(new[] { left }, new[] { right }));
        }

        [Fact]
        public void DifferenceIsIgnoredForMember()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public record First(string a, string b);
  public class Second { }
  public class Third { }
  public class Fourth { }
  public enum MyEnum { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public record First(string a, string b);
}
";

            (string, string)[] ignoredDifferences = new[]
            {
                (DiagnosticIds.TypeMustExist, "T:CompatTests.Second"),
                (DiagnosticIds.TypeMustExist, "T:CompatTests.MyEnum"),
            };

            ApiComparer differ = new();
            differ.IgnoredDifferences = ignoredDifferences;

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Third' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.Third"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Fourth' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.Fourth")
            };

            Assert.Equal(expected, differences);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InternalTypesAreIgnoredWhenSpecified(bool includeInternalSymbols)
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  internal class InternalType { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternalSymbols;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            if (!includeInternalSymbols)
            {
                Assert.Empty(differences);
            }
            else
            {
                CompatDifference[] expected = new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.InternalType' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.InternalType")
                };

                Assert.Equal(expected, differences);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void MissingNestedTypeIsReported(bool includeInternalSymbols)
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested { }
    }
    internal class InternalNested
    {
        internal class DoubleNested { }
    }
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    internal class InternalNested { }
  }
}
";

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternalSymbols;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            List<CompatDifference> expected = new()
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.FirstNested' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.First.FirstNested"),
            };

            if (includeInternalSymbols)
            {
                expected.Add(
                  new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.InternalNested.DoubleNested' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.First.InternalNested.DoubleNested")
                );
            }

            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MultipleRightsMissingTypesReported()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
}
";

            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public class First { }
  public class Second { }
}
",
            @"
namespace CompatTests
{
  public class Second { }
}
",
            @"
namespace CompatTests
{
  public class First { }
}
"};

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            List<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> expected = new()
            {
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-0"), Array.Empty<CompatDifference>()),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-1"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.First"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-2"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Second' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.Second"),
                }),
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MultipleRightsMissingNestedTypesAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
";

            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public class First
  {
  }
}
",
            @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
    }
  }
}
",
            @"
namespace CompatTests
{
}
"};

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            List<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> expected = new()
            {
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-0"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.FirstNested.SecondNested.ThirdNested' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.First.FirstNested.SecondNested.ThirdNested"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-1"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.FirstNested' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.First.FirstNested"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-2"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.FirstNested.SecondNested' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.First.FirstNested.SecondNested"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-3"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.First"),
                }),
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MultipleRightsNoDifferences()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
";

            string[] rightSyntaxes = new[] { leftSyntax, leftSyntax, leftSyntax, leftSyntax };

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            int i = 0;
            foreach ((MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences) diff in differences)
            {
                Assert.Equal(left.MetadataInformation, diff.left);
                MetadataInformation expectedRightMetadata = new(string.Empty, string.Empty, $"runtime-{i++}");
                Assert.Equal(expectedRightMetadata, diff.right);
                Assert.Empty(diff.differences);
            }

            Assert.Equal(4, i);
        }

        [Fact]
        public void MultipleRightsTypeForwardExistsOnAll()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string rightSyntax = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
";
            string[] rightSyntaxes = new[] { rightSyntax, rightSyntax, rightSyntax, rightSyntax, rightSyntax };
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(forwardedTypeSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));
            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes, references, includeDefaultReferences: true);

            ApiComparer differ = new();
            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            int i = 0;
            foreach ((MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences) diff in differences)
            {
                Assert.Equal(left.MetadataInformation, diff.left);
                MetadataInformation expectedRightMetadata = new(string.Empty, string.Empty, $"runtime-{i++}");
                Assert.Equal(expectedRightMetadata, diff.right);
                Assert.Empty(diff.differences);
            }

            Assert.Equal(5, i);
        }

        [Fact]
        public void MultipleRightsMissingTypeForwardIsReported()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string rightWithForward = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
";
            string[] rightSyntaxes = new[] { rightWithForward, "namespace CompatTests { internal class Foo { } }", rightWithForward };
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(forwardedTypeSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));
            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes, references, includeDefaultReferences: true);

            ApiComparer differ = new();
            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            List<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> expected = new()
            {
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-0"), Array.Empty<CompatDifference>()),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-1"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.ForwardedTestType' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.ForwardedTestType"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-2"), Array.Empty<CompatDifference>()),
            };

            Assert.Equal(expected, differences);
        }
    }
}

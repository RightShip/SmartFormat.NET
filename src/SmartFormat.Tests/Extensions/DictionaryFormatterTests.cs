﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
#if !FXCORE
using System.Runtime.Remoting.Messaging;
#endif
using NUnit.Framework;
using SmartFormat.Core.Settings;
using SmartFormat.Extensions;
using SmartFormat.Tests.TestUtils;

namespace SmartFormat.Tests.Extensions
{
    [TestFixture]
    public class DictionaryFormatterTests
    {
        public object[] GetArgs()
        {
            var d = new Dictionary<string, object>() {
                {"Numbers", new Dictionary<string, object>() {
                    {"One", 1},
                    {"Two", 2},
                    {"Three", 3},
                }},
                {"Letters", new Dictionary<string, object>() {
                    {"A", "a"},
                    {"B", "b"},
                    {"C", "c"},
                }},
                {"Object", new {
                    Prop1 = "a",
                    Prop2 = "b",
                    Prop3 = "c",
                }},
            };

            return new object[] {
                d,
            };
        }

        public dynamic GetDynamicArgs()
        {
            dynamic d = new ExpandoObject();
            d.Numbers = new Dictionary<string, object> { { "One", 1 }, { "Two", 2 }, { "Three", 3 }, };
            d.Letters = new Dictionary<string, object> { { "A", "a" }, { "B", "b" }, { "C", "c" }, };
            d.Raw = new Dictionary<string, string>() { { "X", "z" } };
            d.Object = new { Prop1 = "a", Prop2 = "b", Prop3 = "c", };

            return new object[] {
                d,
            };
        }

        [Test]
        public void Test_Dictionary()
        {
            var formatter = Smart.CreateDefaultSmartFormat();
            formatter.AddExtensions(new DictionarySource(formatter));
            formatter.Parser.UseAlternativeEscapeChar(); // curly braces MUST be escaped with \{ and \} instead of {{ and }} for this complex test

            var formats = new string[] {
                "Chained: {0.Numbers.One} {Numbers.Two} {Letters.A} {Object.Prop1}",
                "Nested: {0:{Numbers:{One} {Two}}} {Letters:{A}} {Object:{Prop1}}" 
            };
            var expected = new string[] {
                "Chained: 1 2 a a",
                "Nested: 1 2 a a"
            };
            var args = GetArgs();
            formatter.Test(formats, args, expected);

            formatter.Parser.UseBraceEscaping(); // reset to string.Format brace escaping
        }

        [Test]
        public void Test_Dynamic()
        {
            var formatter = Smart.CreateDefaultSmartFormat();
            formatter.AddExtensions(new DictionarySource(formatter));
            formatter.Parser.UseAlternativeEscapeChar(); // curly braces MUST be escaped with \{ and \} instead of {{ and }} for this complex test

            var formats = new string[] {
                "Chained: {0.Numbers.One} {Numbers.Two} {Letters.A} {Object.Prop1} {Raw.X}",
                "Nested: {0:{Numbers:{One} {Two}}} {Letters:{A}} {Object:{Prop1}} {Raw:{X}}"
            };
            var expected = new string[] {
                "Chained: 1 2 a a z",
                "Nested: 1 2 a a z"
            };
            var args = (object[])GetDynamicArgs();
            formatter.Test(formats, args, expected);

            formatter.Parser.UseBraceEscaping(); // reset to string.Format brace escaping
        }

        [Test]
        public void Test_Dynamic_CaseInsensitive()
        {
            var formatter = Smart.CreateDefaultSmartFormat();
            formatter.Settings.CaseSensitivity = CaseSensitivityType.CaseInsensitive;
            formatter.AddExtensions(new DictionarySource(formatter));
            formatter.Parser.UseAlternativeEscapeChar(); // curly braces MUST be escaped with \{ and \} instead of {{ and }} for this complex test

            var formats = new string[] {
                "Chained: {0.Numbers.One} {Numbers.Two} {Letters.A} {Object.Prop1} {Raw.x}",
                "Nested: {0:{Numbers:{One} {Two}}} {Letters:{A}} {Object:{Prop1}} {Raw:{x}}"
            };
            var expected = new string[] {
                "Chained: 1 2 a a z",
                "Nested: 1 2 a a z"
            };
            var args = (object[])GetDynamicArgs();
            formatter.Test(formats, args, expected);

            formatter.Parser.UseBraceEscaping(); // reset to string.Format brace escaping
        }

        [Test]
        public void Dictionary_Dot_Notation()
        {
            // Process properties of a class instance type-safe and without the need for reflection
            // and with dot notation for dictionaries

            var addr = new Address();

            const string format = "Address: {City.ZipCode} {City.Name}, {City.AreaCode}\n" +
                                  "Name: {Person.FirstName} {Person.LastName}";

            var expected = $"Address: {addr.City.ZipCode} {addr.City.Name}, {addr.City.AreaCode}\n" +
                         $"Name: {addr.Person.FirstName} {addr.Person.LastName}";

            var formatter = Smart.CreateDefaultSmartFormat();
            var result = formatter.Format(format, addr.ToDictionary());

            Assert.AreEqual(expected, result);
        }

        [Test, Explicit("Performance tests should be run explicitly. This test will take about 4 seconds.")]
        public void Dictionary_Performance()
        {
            var result = string.Empty;
            var addr = new Address();
            const string format = "Address: {City.ZipCode} {City.Name}, {City.AreaCode}\n" +
                                  "Name: {Person.FirstName} {Person.LastName}";

            var sw = new Stopwatch();

            // Direct member acess:
            sw.Start();
            for (var i = 0; i < 100000; i++)
            {
                result = $"Address: {addr.City.ZipCode} {addr.City.Name}, {addr.City.AreaCode}\n" +
                         $"Name: {addr.Person.FirstName} {addr.Person.LastName}";
            }
            sw.Stop();
            var directMemberTest = sw.ElapsedMilliseconds;
            sw.Reset();

            // Smart.Format with reflection:
            var formatter = new SmartFormatter();
            formatter.AddExtensions(
                new ReflectionSource(formatter),
                new DefaultSource(formatter)
                );
            formatter.AddExtensions(
                new DefaultFormatter()
                );

            sw.Start();
            for (var i = 0; i < 100000; i++)
            {
                result = formatter.Format(format, addr);
            }
            sw.Stop();
            var reflectionMemberTest = sw.ElapsedMilliseconds;
            sw.Reset();

            // Smart.Format with Dictionary:
            formatter = new SmartFormatter();
            formatter.AddExtensions(
                new DictionarySource(formatter),
                new DefaultSource(formatter)
                );
            formatter.AddExtensions(
                new DefaultFormatter()
                );

            sw.Start();
            var dict = addr.ToDictionary(); // get class projection to dictionary hierarchy
            for (var i = 0; i < 100000; i++)
            {
                result = formatter.Format(format, dict);
            }
            sw.Stop();
            var dictionaryProjectionTest = sw.ElapsedMilliseconds;
            sw.Reset();

            Console.WriteLine("Test results as performance index:");
            Console.WriteLine("Direct Member Test: {0} ({1} ms)", directMemberTest/ directMemberTest, directMemberTest);
            Console.WriteLine("Reflection Test: {0} ({1} ms)", reflectionMemberTest / directMemberTest, reflectionMemberTest);
            Console.WriteLine("Dictionary Projection Test: {0} ({1} ms)", dictionaryProjectionTest / directMemberTest, dictionaryProjectionTest);
        }

        public class Address
        {
            public CityDetails City { get; set; } = new CityDetails();
            public PersonDetails Person { get; set; } = new PersonDetails();

            public Dictionary<string, object> ToDictionary()
            {
                var d = new Dictionary<string, object>
                {
                    { nameof(City), City.ToDictionary() },
                    { nameof(Person), Person.ToDictionary() }
                };
                return d;
            }

            public class CityDetails
            {
                public string Name { get; set; } = "New York";
                public string ZipCode { get; set; } = "00501";
                public string AreaCode { get; set; } = "631";

                public Dictionary<string, string> ToDictionary()
                {
                    return new Dictionary<string, string>
                    {
                        {nameof(Name), Name},
                        {nameof(ZipCode), ZipCode},
                        {nameof(AreaCode), AreaCode}
                    };
                }
            }

            public class PersonDetails
            {
                public string FirstName { get; set; } = "John";
                public string LastName { get; set; } = "Doe";
                public Dictionary<string, string> ToDictionary()
                {
                    return new Dictionary<string, string>
                    {
                        {nameof(FirstName), FirstName},
                        {nameof(LastName), LastName}
                    };
                }
            }
        }
    }
}

﻿/* PetaJson v0.5 - A simple but flexible Json library in a single .cs file.
 *
 * Copyright © 2014 Topten Software.  All Rights Reserved.
 * 
 * Apache License 2.0 - http://www.toptensoftware.com/petapoco/license
 */

// Define PETAJSON_DYNAMIC in your project settings for Expando support

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections;
#if PETAJSON_DYNAMIC
using System.Dynamic;
#endif

namespace PetaJson
{
    // Pass to format/write/parse functions to override defaults
    [Flags]
    public enum JsonOptions
    {
        None = 0,
        WriteWhitespace  = 0x00000001,
        DontWriteWhitespace = 0x00000002,
        StrictParser = 0x00000004,
        NonStrictParser = 0x00000008,
    }

    // API
    public static class Json
    {
        static Json()
        {
            WriteWhitespaceDefault = true;
            StrictParserDefault = false;
        }

        // Pretty format default
        public static bool WriteWhitespaceDefault
        {
            get;
            set;
        }

        // Strict parser
        public static bool StrictParserDefault
        {
            get;
            set;
        }

        // Write an object to a text writer
        public static void Write(TextWriter w, object o, JsonOptions options = JsonOptions.None)
        {
            var writer = new Internal.Writer(w, ResolveOptions(options));
            writer.WriteValue(o);
        }

        // Write an object to a file
        public static void WriteFile(string filename, object o, JsonOptions options = JsonOptions.None)
        {
            using (var w = new StreamWriter(filename))
            {
                Write(w, o, options);
            }
        }

        // Format an object as a json string
        public static string Format(object o, JsonOptions options = JsonOptions.None)
        {
            var sw = new StringWriter();
            var writer = new Internal.Writer(sw, ResolveOptions(options));
            writer.WriteValue(o);
            return sw.ToString();
        }

        // Parse an object of specified type from a text reader
        public static object Parse(TextReader r, Type type, JsonOptions options = JsonOptions.None)
        {
            Internal.Reader reader = null;
            try
            {
                reader = new Internal.Reader(r, ResolveOptions(options));
                var retv = reader.Parse(type);
                reader.CheckEOF();
                return retv;
            }
            catch (Exception x)
            {
                throw new JsonParseException(x, reader==null ? new JsonLineOffset() : reader.CurrentTokenPosition);
            }
        }

        // Parse an object of specified type from a text reader
        public static T Parse<T>(TextReader r, JsonOptions options = JsonOptions.None)
        {
            return (T)Parse(r, typeof(T), options);
        }

        // Parse from text reader into an already instantied object
        public static void ParseInto(TextReader r, Object into, JsonOptions options = JsonOptions.None)
        {
            if (into == null)
                throw new NullReferenceException();
            if (into.GetType().IsValueType)
                throw new InvalidOperationException("Can't ParseInto a value type");

            Internal.Reader reader = null;
            try
            {
                reader = new Internal.Reader(r, ResolveOptions(options));
                reader.ParseInto(into);
                reader.CheckEOF();
            }
            catch (Exception x)
            {
                throw new JsonParseException(x, reader==null ? new JsonLineOffset() : reader.CurrentTokenPosition);
            }
        }

        // Parse an object of specified type from a file
        public static object ParseFile(string filename, Type type, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                return Parse(r, type, options);
            }
        }

        // Parse an object of specified type from a file
        public static T ParseFile<T>(string filename, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                return Parse<T>(r, options);
            }
        }

        // Parse from file into an already instantied object
        public static void ParseFileInto(string filename, Object into, JsonOptions options = JsonOptions.None)
        {
            using (var r = new StreamReader(filename))
            {
                ParseInto(r, into, options);
            }
        }

        // Parse an object from a string
        public static object Parse(string data, Type type, JsonOptions options = JsonOptions.None)
        {
            return Parse(new StringReader(data), type, options);
        }

        // Parse an object from a string
        public static T Parse<T>(string data, JsonOptions options = JsonOptions.None)
        {
            return (T)Parse<T>(new StringReader(data), options);
        }

        // Parse from string into an already instantiated object
        public static void ParseInto(string data, Object into, JsonOptions options = JsonOptions.None)
        {
            ParseInto(new StringReader(data), into, options);
        }

        // Create a clone of an object
        public static T Clone<T>(T source)
        {
            return (T)Clone((object)source);
        }

        // Create a clone of an object (untyped)
        public static object Clone(object source)
        {
            if (source == null)
                return null;

            return Parse(Format(source), source.GetType());
        }

        // Clone an object into another instance
        public static void CloneInto<T>(T dest, T source)
        {
            ParseInto(Format(source), dest);
        }

        // Register a callback that can format a value of a particular type into json
        public static void RegisterFormatter(Type type, Action<IJsonWriter, object> formatter)
        {
            Internal.Writer._formatters[type] = formatter;
        }

        // Typed version of above
        public static void RegisterFormatter<T>(Action<IJsonWriter, T> formatter)
        {
            RegisterFormatter(typeof(T), (w, o) => formatter(w, (T)o));
        }

        // Register a parser for a specified type
        public static void RegisterParser(Type type, Func<IJsonReader, Type, object> parser)
        {
            Internal.Reader._parsers[type] = parser;
        }

        // Register a typed parser
        public static void RegisterParser<T>(Func<IJsonReader, Type, T> parser)
        {
            RegisterParser(typeof(T), (r, t) => parser(r, t));
        }

        // Simpler version for simple types
        public static void RegisterParser(Type type, Func<object, object> parser)
        {
            RegisterParser(type, (r, t) => r.ReadLiteral(parser));
        }

        // Simpler and typesafe parser for simple types
        public static void RegisterParser<T>(Func<object, T> parser)
        {
            RegisterParser(typeof(T), literal => parser(literal));
        }

        // Register an into parser
        public static void RegisterIntoParser(Type type, Action<IJsonReader, object> parser)
        {
            Internal.Reader._intoParsers[type] = parser;
        }

        // Register an into parser
        public static void RegisterIntoParser<T>(Action<IJsonReader, object> parser)
        {
            RegisterIntoParser(typeof(T), parser);
        }

        // Register a factory for instantiating objects (typically abstract classes)
        // Callback will be invoked for each key in the dictionary until it returns an object
        // instance and which point it will switch to serialization using reflection
        public static void RegisterTypeFactory(Type type, Func<IJsonReader, string, object> factory)
        {
            Internal.Reader._typeFactories[type] = factory;
        }

        // Register a callback to provide a formatter for a newly encountered type
        public static void SetFormatterResolver(Func<Type, Action<IJsonWriter, object>> resolver)
        {
            Internal.Writer._formatterResolver = resolver;
        }

        // Register a callback to provide a parser for a newly encountered value type
        public static void SetParserResolver(Func<Type, Func<IJsonReader, Type, object>> resolver)
        {
            Internal.Reader._parserResolver = resolver;
        }

        // Register a callback to provide a parser for a newly encountered reference type
        public static void SetIntoParserResolver(Func<Type, Action<IJsonReader, object>> resolver)
        {
            Internal.Reader._intoParserResolver = resolver;
        }

        // Resolve passed options        
        static JsonOptions ResolveOptions(JsonOptions options)
        {
            JsonOptions resolved = JsonOptions.None;

            if ((options & (JsonOptions.WriteWhitespace|JsonOptions.DontWriteWhitespace))!=0)
                resolved |= options & (JsonOptions.WriteWhitespace | JsonOptions.DontWriteWhitespace);
            else
                resolved |= WriteWhitespaceDefault ? JsonOptions.WriteWhitespace : JsonOptions.DontWriteWhitespace;

            if ((options & (JsonOptions.StrictParser | JsonOptions.NonStrictParser)) != 0)
                resolved |= options & (JsonOptions.StrictParser | JsonOptions.NonStrictParser);
            else
                resolved |= StrictParserDefault ? JsonOptions.StrictParser : JsonOptions.NonStrictParser;

            return resolved;
        }
    }

    // Called before loading via reflection
    public interface IJsonLoading
    {
        void OnJsonLoading(IJsonReader r);
    }

    // Called after loading via reflection
    public interface IJsonLoaded
    {
        void OnJsonLoaded(IJsonReader r);
    }

    // Called for each field while loading from reflection
    // Return true if handled
    public interface IJsonLoadField
    {
        bool OnJsonField(IJsonReader r, string key);
    }

    // Called when about to write using reflection
    public interface IJsonWriting
    {
        void OnJsonWriting(IJsonWriter w);
    }

    // Called after written using reflection
    public interface IJsonWritten
    {
        void OnJsonWritten(IJsonWriter w);
    }

    // Describes the current literal in the json stream
    public enum LiteralKind
    {
        None,
        String,
        Null,
        True,
        False,
        SignedInteger,
        UnsignedInteger,
        FloatingPoint,
    }

    // Passed to registered parsers
    public interface IJsonReader
    {
        object Parse(Type type);
        T Parse<T>();
        void ParseInto(object into);

        object ReadLiteral(Func<object, object> converter);
        void ParseDictionary(Action<string> callback);
        void ParseArray(Action callback);

        LiteralKind GetLiteralKind();
        string GetLiteralString();
        void NextToken();
    }

    // Passed to registered formatters
    public interface IJsonWriter
    {
        void WriteStringLiteral(string str);
        void WriteRaw(string str);
        void WriteArray(Action callback);
        void WriteDictionary(Action callback);
        void WriteValue(object value);
        void WriteElement();
        void WriteKey(string key);
        void WriteKeyNoEscaping(string key);
    }

    // Exception thrown for any parse error
    public class JsonParseException : Exception
    {
        public JsonParseException(Exception inner, JsonLineOffset position) : 
            base(string.Format("Json parse error at {0} - {1}", position, inner.Message), inner)
        {
            Position = position;
        }
        public JsonLineOffset Position;
    }

    // Represents a line and character offset position in the source Json
    public struct JsonLineOffset
    {
        public int Line;
        public int Offset;
        public override string ToString()
        {
            return string.Format("line {0}, character {1}", Line + 1, Offset + 1);
        }
    }

    // Used to decorate fields and properties that should be serialized
    //
    // - [Json] on class or struct causes all public fields and properties to be serialized
    // - [Json] on a public or non-public field or property causes that member to be serialized
    // - [JsonExclude] on a field or property causes that field to be not serialized
    // - A class or struct with no [Json] attribute has all public fields/properties serialized
    // - A class or struct with no [Json] attribute but a [Json] attribute on one or more members only serializes those members
    //
    // Use [Json("keyname")] to explicitly specify the key to be used 
    // [Json] without the keyname will be serialized using the name of the member with the first letter lowercased.
    //
    // [Json(KeepInstance=true)] causes container/subobject types to be serialized into the existing member instance (if not null)
    //
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
    public class JsonAttribute : Attribute
    {
        public JsonAttribute()
        {
            _key = null;
        }

        public JsonAttribute(string key)
        {
            _key = key;
        }

        // Key used to save this field/property
        string _key;
        public string Key
        {
            get { return _key; }
        }

        // If true uses ParseInto to parse into the existing object instance
        // If false, creates a new instance as assigns it to the property
        public bool KeepInstance
        {
            get;
            set;
        }
    }

    // See comments for JsonAttribute above
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class JsonExcludeAttribute : Attribute
    {
        public JsonExcludeAttribute()
        {
        }
    }

    namespace Internal
    {
        public enum Token
        {
            EOF,
            Identifier,
            Literal,
            OpenBrace,
            CloseBrace,
            OpenSquare,
            CloseSquare,
            Equal,
            Colon,
            SemiColon,
            Comma,
        }

        public class Reader : IJsonReader
        {
            static Reader()
            {
                // Setup default resolvers
                _parserResolver = ResolveParser;
                _intoParserResolver = ResolveIntoParser;

                Func<IJsonReader, Type, object> simpleConverter = (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Convert.ChangeType(literal, type, CultureInfo.InvariantCulture));
                };

                // Default type handlers
                _parsers.Add(typeof(string), simpleConverter);
                _parsers.Add(typeof(char), simpleConverter);
                _parsers.Add(typeof(bool), simpleConverter);
                _parsers.Add(typeof(byte), simpleConverter);
                _parsers.Add(typeof(sbyte), simpleConverter);
                _parsers.Add(typeof(short), simpleConverter);
                _parsers.Add(typeof(ushort), simpleConverter);
                _parsers.Add(typeof(int), simpleConverter);
                _parsers.Add(typeof(uint), simpleConverter);
                _parsers.Add(typeof(long), simpleConverter);
                _parsers.Add(typeof(ulong), simpleConverter);
                _parsers.Add(typeof(decimal), simpleConverter);
                _parsers.Add(typeof(float), simpleConverter);
                _parsers.Add(typeof(double), simpleConverter);
                _parsers.Add(typeof(DateTime), (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Utils.FromUnixMilliseconds((long)Convert.ChangeType(literal, typeof(long), CultureInfo.InvariantCulture)));
                });
                _parsers.Add(typeof(byte[]), (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Convert.FromBase64String((string)Convert.ChangeType(literal, typeof(string), CultureInfo.InvariantCulture)));
                });
            }

            public Reader(TextReader r, JsonOptions options)
            {
                _tokenizer = new Tokenizer(r, options);
                _options = options;
            }

            Tokenizer _tokenizer;
            JsonOptions _options;

            static Action<IJsonReader, object> ResolveIntoParser(Type type)
            {
                var ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri != null)
                    return ri.ParseInto;
                else
                    return null;
            }

            static Func<IJsonReader, Type, object> ResolveParser(Type type)
            {
                return (r, t) =>
                {
                    var into = Activator.CreateInstance(type);
                    r.ParseInto(into);
                    return into;
                };
            }

            public JsonLineOffset CurrentTokenPosition
            {
                get { return _tokenizer.CurrentTokenPosition; }
            }

            // ReadLiteral is implemented with a converter callback so that any
            // errors on converting to the target type are thrown before the tokenizer
            // is advanced to the next token.  This ensures error location is reported 
            // at the start of the literal, not the following token.
            public object ReadLiteral(Func<object, object> converter)
            {
                _tokenizer.Check(Token.Literal);
                var retv = converter(_tokenizer.LiteralValue);
                _tokenizer.NextToken();
                return retv;
            }

            public void CheckEOF()
            {
                _tokenizer.Check(Token.EOF);
            }

            public object Parse(Type type)
            {
                // Null?
                if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.LiteralKind == LiteralKind.Null)
                {
                    _tokenizer.NextToken();
                    return null;
                }

                // Handle nullable types
                var typeUnderlying = Nullable.GetUnderlyingType(type);
                if (typeUnderlying != null)
                    type = typeUnderlying;

                // See if we have a reader
                Func<IJsonReader, Type, object> parser;
                if (Reader._parsers.TryGetValue(type, out parser))
                {
                    return parser(this, type);
                }

                // See if we have factory
                Func<IJsonReader, string, object> factory;
                if (Reader._typeFactories.TryGetValue(type, out factory))
                {
                    // Try first without passing dictionary keys
                    object into = factory(this, null);
                    if (into == null)
                    {
                        // This is a awkward situation.  The factory requires a value from the dictionary
                        // in order to create the target object (typically an abstract class with the class
                        // kind recorded in the Json).  Since there's no guarantee of order in a json dictionary
                        // we can't assume the required key is first.
                        // So, create a bookmark on the tokenizer, read keys until the factory returns an
                        // object instance and then rewind the tokenizer and continue

                        // Create a bookmark so we can rewind
                        _tokenizer.CreateBookmark();

                        // Skip the opening brace
                        _tokenizer.Skip(Token.OpenBrace);

                        // First pass to work out type
                        ParseDictionaryKeys(key =>
                        {
                            // Try to instantiate the object
                            into = factory(this, key);
                            return into == null;
                        });

                        // Move back to start of the dictionary
                        _tokenizer.RewindToBookmark();

                        // Quit if still didn't get an object from the factory
                        if (into == null)
                            throw new InvalidOperationException("Factory didn't create object instance (probably due to a missing key in the Json)");
                    }

                    // Second pass
                    ParseInto(into);

                    // Done
                    return into;
                }

                // Do we already have an into parser?
                Action<IJsonReader, object> intoParser;
                if (Reader._intoParsers.TryGetValue(type, out intoParser))
                {
                    var into = Activator.CreateInstance(type);
                    ParseInto(into);
                    return into;
                }

                // Enumerated type?
                if (type.IsEnum)
                {
                    return ReadLiteral(literal => Enum.Parse(type, (string)literal));
                }

                // Array?
                if (type.IsArray && type.GetArrayRank() == 1)
                {
                    // First parse as a List<>
                    var listType = typeof(List<>).MakeGenericType(type.GetElementType());
                    var list = Activator.CreateInstance(listType);
                    ParseInto(list);

                    return listType.GetMethod("ToArray").Invoke(list, null);
                }

                // Untyped dictionary?
                if (_tokenizer.CurrentToken == Token.OpenBrace && (type.IsAssignableFrom(typeof(Dictionary<string, object>))))
                {
#if PETAJSON_DYNAMIC
                    var container = (new ExpandoObject()) as IDictionary<string, object>;
#else
                    var container = new Dictionary<string, object>();
#endif
                    ParseDictionary(key =>
                    {
                        container[key] = Parse(typeof(Object));
                    });

                    return container;
                }

                // Untyped list?
                if (_tokenizer.CurrentToken == Token.OpenSquare && (type.IsAssignableFrom(typeof(List<object>))))
                {
                    var container = new List<object>();
                    ParseArray(() =>
                    {
                        container.Add(Parse(typeof(Object)));
                    });
                    return container;
                }

                // Untyped literal?
                if (_tokenizer.CurrentToken == Token.Literal && type.IsAssignableFrom(_tokenizer.LiteralType))
                {
                    var lit = _tokenizer.LiteralValue;
                    _tokenizer.NextToken();
                    return lit;
                }

                // Call value type resolver
                if (type.IsValueType)
                {
                    var tp = _parserResolver(type);
                    if (tp != null)
                    {
                        _parsers[type] = tp;
                        return tp(this, type);
                    }
                }

                // Call reference type resolver
                if (type.IsClass && type != typeof(object))
                {
                    var into = Activator.CreateInstance(type);
                    ParseInto(into);
                    return into;
                }

                // Give up
                throw new InvalidDataException(string.Format("syntax error - unexpected token {0}", _tokenizer.CurrentToken));
            }

            // Parse into an existing object instance
            public void ParseInto(object into)
            {
                if (into == null)
                    return;

                var type = into.GetType();

                // Existing parse into handler?
                Action<IJsonReader,object> parseInto;
                if (_intoParsers.TryGetValue(type, out parseInto))
                {
                    parseInto(this, into);
                    return;
                }

                // Generic dictionary?
                var dictType = Utils.FindGenericInterface(type, typeof(IDictionary<,>));
                if (dictType!=null)
                {
                    // Get the key and value types
                    var typeKey = dictType.GetGenericArguments()[0];
                    var typeValue = dictType.GetGenericArguments()[1];

                    // Parse it
                    IDictionary dict = (IDictionary)into;
                    dict.Clear();
                    ParseDictionary(key =>
                    {
                        dict.Add(Convert.ChangeType(key, typeKey), Parse(typeValue));
                    });

                    return;
                }

                // Generic list
                var listType = Utils.FindGenericInterface(type, typeof(IList<>));
                if (listType!=null)
                {
                    // Get element type
                    var typeElement = listType.GetGenericArguments()[0];

                    // Parse it
                    IList list = (IList)into;
                    list.Clear();
                    ParseArray(() =>
                    {
                        list.Add(Parse(typeElement));
                    });

                    return;
                }

                // Untyped dictionary
                var objDict = into as IDictionary;
                if (objDict != null)
                {
                    objDict.Clear();
                    ParseDictionary(key =>
                    {
                        objDict[key] = Parse(typeof(Object));
                    });
                    return;
                }

                // Untyped list
                var objList = into as IList;
                if (objList!=null)
                {
                    objList.Clear();
                    ParseArray(() =>
                    {
                        objList.Add(Parse(typeof(Object)));
                    });
                    return;
                }

                // Try to resolve a parser
                var intoParser = _intoParserResolver(type);
                if (intoParser != null)
                {
                    _intoParsers[type] = intoParser;
                    intoParser(this, into);
                    return;
                }

                throw new InvalidOperationException(string.Format("Don't know how to parse into type '{0}'", type.FullName));
            }

            public T Parse<T>()
            {
                return (T)Parse(typeof(T));
            }

            public LiteralKind GetLiteralKind() 
            { 
                return _tokenizer.LiteralKind; 
            }
            
            public string GetLiteralString() 
            { 
                return _tokenizer.String; 
            }

            public void NextToken() 
            { 
                _tokenizer.NextToken(); 
            }

            // Parse a dictionary
            public void ParseDictionary(Action<string> callback)
            {
                _tokenizer.Skip(Token.OpenBrace);
                ParseDictionaryKeys(key => { callback(key); return true; });
                _tokenizer.Skip(Token.CloseBrace);
            }

            // Parse dictionary keys, calling callback for each one.  Continues until end of input
            // or when callback returns false
            private void ParseDictionaryKeys(Func<string, bool> callback)
            {
                // End?
                while (_tokenizer.CurrentToken != Token.CloseBrace)
                {
                    // Parse the key
                    string key = null;
                    if (_tokenizer.CurrentToken == Token.Identifier && (_options & JsonOptions.StrictParser)==0)
                    {
                        key = _tokenizer.String;
                    }
                    else if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.LiteralKind == LiteralKind.String)
                    {
                        key = (string)_tokenizer.LiteralValue;
                    }
                    else
                    {
                        throw new InvalidDataException("syntax error, expected string literal or identifier");
                    }
                    _tokenizer.NextToken();
                    _tokenizer.Skip(Token.Colon);

                    // Remember current position
                    var pos = _tokenizer.CurrentTokenPosition;

                    // Call the callback, quit if cancelled
                    if (!callback(key))
                        return;

                    // If the callback didn't read anything from the tokenizer, then skip it ourself
                    if (pos.Line == _tokenizer.CurrentTokenPosition.Line && pos.Offset == _tokenizer.CurrentTokenPosition.Offset)
                    {
                        Parse(typeof(object));
                    }

                    // Separating/trailing comma
                    if (_tokenizer.SkipIf(Token.Comma))
                    {
                        if ((_options & JsonOptions.StrictParser) != 0 && _tokenizer.CurrentToken == Token.CloseBrace)
                        {
                            throw new InvalidDataException("Trailing commas not allowed in strict mode");
                        }
                        continue;
                    }

                    // End
                    break;
                }
            }

            // Parse an array
            public void ParseArray(Action callback)
            {
                _tokenizer.Skip(Token.OpenSquare);

                while (_tokenizer.CurrentToken != Token.CloseSquare)
                {
                    callback();

                    if (_tokenizer.SkipIf(Token.Comma))
                    {
                        if ((_options & JsonOptions.StrictParser)!=0 && _tokenizer.CurrentToken==Token.CloseSquare)
                        {
                            throw new InvalidDataException("Trailing commas not allowed in strict mode");
                        }
                        continue;
                    }
                    break;
                }

                _tokenizer.Skip(Token.CloseSquare);
            }

            // Yikes!
            public static Func<Type, Action<IJsonReader, object>> _intoParserResolver;
            public static Func<Type, Func<IJsonReader, Type, object>> _parserResolver;
            public static Dictionary<Type, Func<IJsonReader, Type, object>> _parsers = new Dictionary<Type, Func<IJsonReader, Type, object>>();
            public static Dictionary<Type, Action<IJsonReader, object>> _intoParsers = new Dictionary<Type, Action<IJsonReader, object>>();
            public static Dictionary<Type, Func<IJsonReader, string, object>> _typeFactories = new Dictionary<Type, Func<IJsonReader, string, object>>();
        }

        public class Writer : IJsonWriter
        {
            static Writer()
            {
                _formatterResolver = ResolveFormatter;

                // Register standard formatters
                _formatters.Add(typeof(string), (w, o) => w.WriteStringLiteral((string)o));
                _formatters.Add(typeof(char), (w, o) => w.WriteStringLiteral(((char)o).ToString()));
                _formatters.Add(typeof(bool), (w, o) => w.WriteRaw(((bool)o) ? "true" : "false"));
                Action<IJsonWriter, object> convertWriter = (w, o) => w.WriteRaw((string)Convert.ChangeType(o, typeof(string), System.Globalization.CultureInfo.InvariantCulture));
                _formatters.Add(typeof(int), convertWriter);
                _formatters.Add(typeof(uint), convertWriter);
                _formatters.Add(typeof(long), convertWriter);
                _formatters.Add(typeof(ulong), convertWriter);
                _formatters.Add(typeof(short), convertWriter);
                _formatters.Add(typeof(ushort), convertWriter);
                _formatters.Add(typeof(decimal), convertWriter);
                _formatters.Add(typeof(byte), convertWriter);
                _formatters.Add(typeof(sbyte), convertWriter);
                _formatters.Add(typeof(DateTime), (w, o) => convertWriter(w, Utils.ToUnixMilliseconds((DateTime)o)));
                _formatters.Add(typeof(float), (w, o) => w.WriteRaw(((float)o).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                _formatters.Add(typeof(double), (w, o) => w.WriteRaw(((double)o).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                _formatters.Add(typeof(byte[]), (w, o) =>
                {
                    w.WriteRaw("\"");
                    w.WriteRaw(Convert.ToBase64String((byte[])o));
                    w.WriteRaw("\"");
                });
            }

            public static Func<Type, Action<IJsonWriter, object>> _formatterResolver;
            public static Dictionary<Type, Action<IJsonWriter, object>> _formatters = new Dictionary<Type, Action<IJsonWriter, object>>();

            static Action<IJsonWriter, object> ResolveFormatter(Type type)
            {
                var ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri != null)
                    return ri.Write;
                else
                    return null;
            }

            public Writer(TextWriter w, JsonOptions options)
            {
                _writer = w;
                _atStartOfLine = true;
                _needElementSeparator = false;
                _options = options;
            }

            private TextWriter _writer;
            private int IndentLevel;
            private bool _atStartOfLine;
            private bool _needElementSeparator = false;
            private JsonOptions _options;
            private char _currentBlockKind = '\0';

            // Move to the next line
            public void NextLine()
            {
                if (_atStartOfLine)
                    return;

                if ((_options & JsonOptions.WriteWhitespace)!=0)
                {
                    WriteRaw("\n");
                    WriteRaw(new string('\t', IndentLevel));
                }
                _atStartOfLine = true;
            }

            // Start the next element, writing separators and white space
            void NextElement()
            {
                if (_needElementSeparator)
                {
                    WriteRaw(",");
                    NextLine();
                }
                else
                {
                    NextLine();
                    IndentLevel++;
                    WriteRaw(_currentBlockKind.ToString());
                    NextLine();
                }

                _needElementSeparator = true;
            }

            // Write next array element
            public void WriteElement()
            {
                if (_currentBlockKind != '[')
                    throw new InvalidOperationException("Attempt to write array element when not in array block");
                NextElement();
            }

            // Write next dictionary key
            public void WriteKey(string key)
            {
                if (_currentBlockKind != '{')
                    throw new InvalidOperationException("Attempt to write dictionary element when not in dictionary block");
                NextElement();
                WriteStringLiteral(key);
                WriteRaw(((_options & JsonOptions.WriteWhitespace) != 0) ? ": " : ":");
            }

            // Write an already escaped dictionary key
            public void WriteKeyNoEscaping(string key)
            {
                if (_currentBlockKind != '{')
                    throw new InvalidOperationException("Attempt to write dictionary element when not in dictionary block");
                NextElement();
                WriteRaw("\"");
                WriteRaw(key);
                WriteRaw("\"");
                WriteRaw(((_options & JsonOptions.WriteWhitespace) != 0) ? ": " : ":");
            }

            // Write anything
            public void WriteRaw(string str)
            {
                _atStartOfLine = false;
                _writer.Write(str);
            }

            // Write a string, escaping as necessary
            static char[] _charsToEscape = new char[] { '\"', '\r', '\n', '\t', '\f', '\0', '\\', '\'' };
            public void WriteStringLiteral(string str)
            {
                _writer.Write("\"");

                int pos = 0;
                int escapePos;
                while ((escapePos = str.IndexOfAny(_charsToEscape, pos)) >= 0)
                {
                    if (escapePos > pos)
                        _writer.Write(str.Substring(pos, escapePos - pos));

                    switch (str[escapePos])
                    {
                        case '\"': _writer.Write("\\\""); break;
                        case '\r': _writer.Write("\\r"); break;
                        case '\n': _writer.Write("\\n"); break;
                        case '\t': _writer.Write("\\t"); break;
                        case '\f': _writer.Write("\\f"); break;
                        case '\0': _writer.Write("\\0"); break;
                        case '\\': _writer.Write("\\\\"); break;
                        case '\'': _writer.Write("\\'"); break;
                    }

                    pos = escapePos + 1;
                }


                if (str.Length > pos)
                    _writer.Write(str.Substring(pos));
                _writer.Write("\"");
            }

            // Write an array or dictionary block
            private void WriteBlock(string open, string close, Action callback)
            {
                var prevBlockKind = _currentBlockKind;
                _currentBlockKind = open[0];

                var didNeedElementSeparator = _needElementSeparator;
                _needElementSeparator = false;

                callback();

                if (_needElementSeparator)
                {
                    IndentLevel--;
                    NextLine();
                }
                else
                {
                    WriteRaw(open);
                }
                WriteRaw(close);

                _needElementSeparator = didNeedElementSeparator;
                _currentBlockKind = prevBlockKind;
            }

            // Write an array
            public void WriteArray(Action callback)
            {
                WriteBlock("[", "]", callback);
            }

            // Write a dictionary
            public void WriteDictionary(Action callback)
            {
                WriteBlock("{", "}", callback);
            }

            // Write any value
            public void WriteValue(object value)
            {
                // Special handling for null
                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }

                var type = value.GetType();

                // Handle nullable types
                var typeUnderlying = Nullable.GetUnderlyingType(type);
                if (typeUnderlying != null)
                    type = typeUnderlying;

                // Look up type writer
                Action<IJsonWriter, object> typeWriter;
                if (_formatters.TryGetValue(type, out typeWriter))
                {
                    // Write it
                    typeWriter(this, value);
                    return;
                }

                // Enumerated type?
                if (type.IsEnum)
                {
                    WriteStringLiteral(value.ToString());
                    return;
                }

                // Dictionary?
                var d = value as System.Collections.IDictionary;
                if (d != null)
                {
                    WriteDictionary(() =>
                    {
                        foreach (var key in d.Keys)
                        {
                            WriteKey(key.ToString());
                            WriteValue(d[key]);
                        }
                    });
                    return;
                }

                // Array?
                var e = value as System.Collections.IEnumerable;
                if (e != null)
                {
                    WriteArray(() =>
                    {
                        foreach (var i in e)
                        {
                            WriteElement();
                            WriteValue(i);
                        }
                    });
                    return;
                }

                // Resolve a formatter
                var formatter = _formatterResolver(type);
                if (formatter != null)
                {
                    _formatters[type] = formatter;
                    formatter(this, value);
                    return;
                }

                // Give up
                throw new InvalidDataException(string.Format("Don't know how to write '{0}' to json", value.GetType()));
            }
        }

        // Information about a field or property found through reflection
        public class JsonMemberInfo
        {
            // The Json key for this member
            public string JsonKey;

            // True if should keep existing instance (reference types only)
            public bool KeepInstance;

            // Reflected member info
            MemberInfo _mi;
            public MemberInfo Member
            {
                get { return _mi; }
                set
                {
                    // Store it
                    _mi = value;

                    // Also create getters and setters
                    if (_mi is PropertyInfo)
                    {
                        GetValue = (obj) => ((PropertyInfo)_mi).GetValue(obj, null);
                        SetValue = (obj, val) => ((PropertyInfo)_mi).SetValue(obj, val, null);
                    }
                    else
                    {
                        GetValue = ((FieldInfo)_mi).GetValue;
                        SetValue = ((FieldInfo)_mi).SetValue;
                    }
                }
            }

            // Member type
            public Type MemberType
            {
                get
                {
                    if (Member is PropertyInfo)
                    {
                        return ((PropertyInfo)Member).PropertyType;
                    }
                    else
                    {
                        return ((FieldInfo)Member).FieldType;
                    }
                }
            }

            // Get/set helpers
            public Action<object, object> SetValue;
            public Func<object, object> GetValue;
        }

        // Stores reflection info about a type
        public class ReflectionInfo
        {
            // List of members to be serialized
            public List<JsonMemberInfo> Members;

            // Cache of these ReflectionInfos's
            static Dictionary<Type, ReflectionInfo> _cache = new Dictionary<Type, ReflectionInfo>();

            // Write one of these types
            public void Write(IJsonWriter w, object val)
            {
                w.WriteDictionary(() =>
                {
                    var writing = val as IJsonWriting;
                    if (writing != null)
                        writing.OnJsonWriting(w);

                    foreach (var jmi in Members)
                    {
                        w.WriteKeyNoEscaping(jmi.JsonKey);
                        w.WriteValue(jmi.GetValue(val));
                    }

                    var written = val as IJsonWritten;
                    if (written != null)
                        written.OnJsonWritten(w);
                });
            }

            // Read one of these types.
            // NB: Although PetaJson.JsonParseInto only works on reference type, when using reflection
            //     it also works for value types so we use the one method for both
            public void ParseInto(IJsonReader r, object into)
            {
                var loading = into as IJsonLoading;
                if (loading != null)
                    loading.OnJsonLoading(r);

                r.ParseDictionary(key =>
                {
                    ParseFieldOrProperty(r, into, key);
                });

                var loaded = into as IJsonLoaded;
                if (loaded != null)
                    loaded.OnJsonLoaded(r);
            }

            // The member info is stored in a list (as opposed to a dictionary) so that
            // the json is written in the same order as the fields/properties are defined
            // On loading, we assume the fields will be in the same order, but need to
            // handle if they're not.  This function performs a linear search, but
            // starts after the last found item as an optimization that should work
            // most of the time.
            int _lastFoundIndex = 0;
            bool FindMemberInfo(string name, out JsonMemberInfo found)
            {
                for (int i = 0; i < Members.Count; i++)
                {
                    int index = (i + _lastFoundIndex) % Members.Count;
                    var jmi = Members[index];
                    if (jmi.JsonKey == name)
                    {
                        _lastFoundIndex = index;
                        found = jmi;
                        return true;
                    }
                }
                found = null;
                return false;
            }

            // Parse a value from IJsonReader into an object instance
            public void ParseFieldOrProperty(IJsonReader r, object into, string key)
            {
                // IJsonLoadField
                var lf = into as IJsonLoadField;
                if (lf != null && lf.OnJsonField(r, key))
                    return;

                // Find member
                JsonMemberInfo jmi;
                if (FindMemberInfo(key, out jmi))
                {
                    // Try to keep existing instance
                    if (jmi.KeepInstance)
                    {
                        var subInto = jmi.GetValue(into);
                        if (subInto != null)
                        {
                            r.ParseInto(subInto);
                            return;
                        }
                    }

                    // Parse and set
                    var val = r.Parse(jmi.MemberType);
                    jmi.SetValue(into, val);
                    return;
                }
            }

            // Get the reflection info for a specified type
            public static ReflectionInfo GetReflectionInfo(Type type)
            {
                // Already created?
                ReflectionInfo existing;
                if (_cache.TryGetValue(type, out existing))
                    return existing;

                // Does type have a [Json] attribute
                bool typeMarked = type.GetCustomAttributes(typeof(JsonAttribute), true).OfType<JsonAttribute>().Any();

                // Do any members have a [Json] attribute
                bool anyFieldsMarked = Utils.GetAllFieldsAndProperties(type).Any(x => x.GetCustomAttributes(typeof(JsonAttribute), false).OfType<JsonAttribute>().Any());

                // Should we serialize all public methods?
                bool serializeAllPublics = typeMarked || !anyFieldsMarked;

                // Build 
                var ri = CreateReflectionInfo(type, mi =>
                {
                    // Explicitly excluded?
                    if (mi.GetCustomAttributes(typeof(JsonExcludeAttribute), false).OfType<JsonExcludeAttribute>().Any())
                        return null;

                    // Get attributes
                    var attr = mi.GetCustomAttributes(typeof(JsonAttribute), false).OfType<JsonAttribute>().FirstOrDefault();
                    if (attr != null)
                    {
                        return new JsonMemberInfo()
                        {
                            Member = mi,
                            JsonKey = attr.Key ?? mi.Name.Substring(0, 1).ToLower() + mi.Name.Substring(1),
                            KeepInstance = attr.KeepInstance,
                        };
                    }

                    // Serialize all publics?
                    if (serializeAllPublics && Utils.IsPublic(mi))
                    {
                        return new JsonMemberInfo()
                        {
                            Member = mi,
                            JsonKey = mi.Name.Substring(0, 1).ToLower() + mi.Name.Substring(1),
                        };
                    }

                    return null;
                });

                // Cache it
                _cache[type] = ri;
                return ri;
            }

            public static ReflectionInfo CreateReflectionInfo(Type type, Func<MemberInfo, JsonMemberInfo> callback)
            {
                // Work out properties and fields
                var members = Utils.GetAllFieldsAndProperties(type).Select(x => callback(x)).Where(x => x != null).ToList();

                // Anything with KeepInstance must be a reference type
                var invalid = members.FirstOrDefault(x => x.KeepInstance && x.MemberType.IsValueType);
                if (invalid!=null)
                {
                    throw new InvalidOperationException(string.Format("KeepInstance=true can only be applied to reference types ({0}.{1})", type.FullName, invalid.Member));
                }

                // Must have some members
                if (!members.Any())
                    return null;

                // Create reflection info
                return new ReflectionInfo() { Members = members };
            }
        }

        internal static class Utils
        {
            // Get all fields and properties of a type
            public static IEnumerable<MemberInfo> GetAllFieldsAndProperties(Type t)
            {
                if (t == null)
                    return Enumerable.Empty<FieldInfo>();

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                return t.GetMembers(flags).Where(x => x is FieldInfo || x is PropertyInfo).Concat(GetAllFieldsAndProperties(t.BaseType));
            }

            public static Type FindGenericInterface(Type type, Type tItf)
            {
                foreach (var t in type.GetInterfaces())
                {
                    // Is this a generic list?
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == tItf)
                        return type;
                }

                return null;
            }

            public static bool IsPublic(MemberInfo mi)
            {
                // Public field
                var fi = mi as FieldInfo;
                if (fi != null)
                    return fi.IsPublic;

                // Public property
                // (We only check the get method so we can work with anonymous types)
                var pi = mi as PropertyInfo;
                if (pi != null)
                {
                    var gm = pi.GetGetMethod();
                    return (gm != null && gm.IsPublic);
                }

                return false;
            }

            public static long ToUnixMilliseconds(DateTime This)
            {
                return (long)This.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            }

            public static DateTime FromUnixMilliseconds(long timeStamp)
            {
                return new DateTime(1970, 1, 1).AddMilliseconds(timeStamp);
            }

        }

        public class Tokenizer
        {
            public Tokenizer(TextReader r, JsonOptions options)
            {
                _underlying = r;
                _options = options;
                FillBuffer();
                NextChar();
                NextToken();
            }

            private JsonOptions _options;
            private StringBuilder _sb = new StringBuilder();
            private TextReader _underlying;
            private char[] _buf = new char[4096];
            private int _pos;
            private int _bufUsed;
            private StringBuilder _rewindBuffer;
            private int _rewindBufferPos;
            private JsonLineOffset _currentCharPos;
            private char _currentChar;
            private Stack<ReaderState> _bookmarks = new Stack<ReaderState>();

            public JsonLineOffset CurrentTokenPosition;
            public Token CurrentToken;
            public LiteralKind LiteralKind;
            public string String;

            public object LiteralValue
            {
                get
                {
                    if (CurrentToken != Token.Literal)
                        throw new InvalidOperationException("token is not a literal");
                    switch (LiteralKind)
                    {
                        case LiteralKind.Null: return null;
                        case LiteralKind.False: return false;
                        case LiteralKind.True: return true;
                        case LiteralKind.String: return String;
                        case LiteralKind.SignedInteger: return long.Parse(String, CultureInfo.InvariantCulture);
                        case LiteralKind.UnsignedInteger: return ulong.Parse(String, CultureInfo.InvariantCulture);
                        case LiteralKind.FloatingPoint: return double.Parse(String, CultureInfo.InvariantCulture);
                    }
                    return null;
                }
            }

            public Type LiteralType
            {
                get
                {
                    if (CurrentToken != Token.Literal)
                        throw new InvalidOperationException("token is not a literal");
                    switch (LiteralKind)
                    {
                        case LiteralKind.Null: return typeof(Object);
                        case LiteralKind.False: return typeof(Boolean);
                        case LiteralKind.True: return typeof(Boolean);
                        case LiteralKind.String: return typeof(string);
                        case LiteralKind.SignedInteger: return typeof(long);
                        case LiteralKind.UnsignedInteger: return typeof(ulong);
                        case LiteralKind.FloatingPoint: return typeof(double);
                    }

                    return null;
                }
            }

            // This object represents the entire state of the reader and is used for rewind
            struct ReaderState
            {
                public ReaderState(Tokenizer tokenizer)
                {
                    _currentCharPos = tokenizer._currentCharPos;
                    _currentChar = tokenizer._currentChar;
                    _string = tokenizer.String;
                    _literalKind = tokenizer.LiteralKind;
                    _rewindBufferPos = tokenizer._rewindBufferPos;
                    _currentTokenPos = tokenizer.CurrentTokenPosition;
                    _currentToken = tokenizer.CurrentToken;
                }

                public void Apply(Tokenizer tokenizer)
                {
                    tokenizer._currentCharPos = _currentCharPos;
                    tokenizer._currentChar = _currentChar;
                    tokenizer._rewindBufferPos = _rewindBufferPos;
                    tokenizer.CurrentToken = _currentToken;
                    tokenizer.CurrentTokenPosition = _currentTokenPos;
                    tokenizer.String = _string;
                    tokenizer.LiteralKind = _literalKind;
                }

                private JsonLineOffset _currentCharPos;
                private JsonLineOffset _currentTokenPos;
                private char _currentChar;
                private Token _currentToken;
                private LiteralKind _literalKind;
                private string _string;
                private int _rewindBufferPos;
            }

            // Create a rewind bookmark
            public void CreateBookmark()
            {
                _bookmarks.Push(new ReaderState(this));
                if (_rewindBuffer == null)
                {
                    _rewindBuffer = new StringBuilder();
                    _rewindBufferPos = 0;
                }
            }

            // Discard bookmark
            public void DiscardBookmark()
            {
                _bookmarks.Pop();
                if (_bookmarks.Count == 0)
                {
                    _rewindBuffer = null;
                    _rewindBufferPos = 0;
                }
            }

            // Rewind to a bookmark
            public void RewindToBookmark()
            {
                _bookmarks.Pop().Apply(this);
            }

            // Fill buffer by reading from underlying TextReader
            void FillBuffer()
            {
                _bufUsed = _underlying.Read(_buf, 0, _buf.Length);
                _pos = 0;
            }

            // Get the next character from the input stream
            // (this function could be extracted into a few different methods, but is mostly inlined
            //  for performance - yes it makes a difference)
            public char NextChar()
            {
                if (_rewindBuffer == null)
                {
                    if (_pos >= _bufUsed)
                    {
                        if (_bufUsed > 0)
                        {
                            FillBuffer();
                        }
                        if (_bufUsed == 0)
                        {
                            return _currentChar = '\0';
                        }
                    }

                    // Next
                    _currentCharPos.Offset++;
                    return _currentChar = _buf[_pos++];
                }

                if (_rewindBufferPos < _rewindBuffer.Length)
                {
                    _currentCharPos.Offset++;
                    return _currentChar = _rewindBuffer[_rewindBufferPos++];
                }
                else
                {
                    if (_pos >= _bufUsed && _bufUsed > 0)
                        FillBuffer();

                    _currentChar = _bufUsed == 0 ? '\0' : _buf[_pos++];
                    _rewindBuffer.Append(_currentChar);
                    _rewindBufferPos++;
                    _currentCharPos.Offset++;
                    return _currentChar;
                }
            }

            // Read the next token from the input stream
            // (Mostly inline for performance)
            public void NextToken()
            {
                while (true)
                {
                    // Skip whitespace and handle line numbers
                    while (true)
                    {
                        if (_currentChar == '\r')
                        {
                            if (NextChar() == '\n')
                            {
                                NextChar();
                            }
                            _currentCharPos.Line++;
                            _currentCharPos.Offset = 0;
                        }
                        else if (_currentChar == '\n')
                        {
                            if (NextChar() == '\r')
                            {
                                NextChar();
                            }
                            _currentCharPos.Line++;
                            _currentCharPos.Offset = 0;
                        }
                        else if (_currentChar == ' ')
                        {
                            NextChar();
                        }
                        else if (_currentChar == '\t')
                        {
                            NextChar();
                        }
                        else
                            break;
                    }
                    
                    // Remember position of token
                    CurrentTokenPosition = _currentCharPos;

                    // Handle common characters first
                    switch (_currentChar)
                    {
                        case '/':
                            // Comments not support in strict mode
                            if ((_options & JsonOptions.StrictParser) != 0)
                            {
                                throw new InvalidDataException(string.Format("syntax error - unexpected character '{0}'", _currentChar));
                            }

                            // Process comment
                            NextChar();
                            switch (_currentChar)
                            {
                                case '/':
                                    NextChar();
                                    while (_currentChar!='\0' && _currentChar != '\r' && _currentChar != '\n')
                                    {
                                        NextChar();
                                    }
                                    break;

                                case '*':
                                    bool endFound = false;
                                    while (!endFound && _currentChar!='\0')
                                    {
                                        if (_currentChar == '*')
                                        {
                                            NextChar();
                                            if (_currentChar == '/')
                                            {
                                                endFound = true;
                                            }
                                        }
                                        NextChar();
                                    }
                                    break;

                                default:
                                    throw new InvalidDataException("syntax error - unexpected character after slash");
                            }
                            continue;

                        case '\"':
                        case '\'':
                        {
                            _sb.Length = 0;
                            var quoteKind = _currentChar;
                            NextChar();
                            while (_currentChar!='\0')
                            {
                                if (_currentChar == '\\')
                                {
                                    NextChar();
                                    var escape = _currentChar;
                                    switch (escape)
                                    {
                                        case '\'': _sb.Append('\''); break;
                                        case '\"': _sb.Append('\"'); break;
                                        case '\\': _sb.Append('\\'); break;
                                        case 'r': _sb.Append('\r'); break;
                                        case 'f': _sb.Append('\f'); break;
                                        case 'n': _sb.Append('\n'); break;
                                        case 't': _sb.Append('\t'); break;
                                        case '0': _sb.Append('\0'); break;
                                        case 'u':
                                            var sbHex = new StringBuilder();
                                            for (int i = 0; i < 4; i++)
                                            {
                                                NextChar();
                                                sbHex.Append(_currentChar);
                                            }
                                            _sb.Append((char)Convert.ToUInt16(sbHex.ToString(), 16));
                                            break;

                                        default:
                                            throw new InvalidDataException(string.Format("Invalid escape sequence in string literal: '\\{0}'", _currentChar));
                                    }
                                }
                                else if (_currentChar == quoteKind)
                                {
                                    String = _sb.ToString();
                                    CurrentToken = Token.Literal;
                                    LiteralKind = LiteralKind.String;
                                    NextChar();
                                    return;
                                }
                                else
                                {
                                    _sb.Append(_currentChar);
                                }

                                NextChar();
                            }
                            throw new InvalidDataException("syntax error - unterminated string literal");
                        }

                        case '{': CurrentToken =  Token.OpenBrace; NextChar(); return;
                        case '}': CurrentToken =  Token.CloseBrace; NextChar(); return;
                        case '[': CurrentToken =  Token.OpenSquare; NextChar(); return;
                        case ']': CurrentToken =  Token.CloseSquare; NextChar(); return;
                        case '=': CurrentToken =  Token.Equal; NextChar(); return;
                        case ':': CurrentToken =  Token.Colon; NextChar(); return;
                        case ';': CurrentToken =  Token.SemiColon; NextChar(); return;
                        case ',': CurrentToken =  Token.Comma; NextChar(); return;
                        case '\0': CurrentToken = Token.EOF; return;
                    }

                    // Number?
                    if (char.IsDigit(_currentChar) || _currentChar == '-')
                    {
                        TokenizeNumber();
                        return;
                    }

                    // Identifier?  (checked for after everything else as identifiers are actually quite rare in valid json)
                    if (Char.IsLetter(_currentChar) || _currentChar == '_' || _currentChar == '$')
                    {
                        // Find end of identifier
                        _sb.Length = 0;
                        while (Char.IsLetterOrDigit(_currentChar) || _currentChar == '_' || _currentChar == '$')
                        {
                            _sb.Append(_currentChar);
                            NextChar();
                        }
                        String = _sb.ToString();

                        // Handle special identifiers
                        switch (String)
                        {
                            case "true":
                                LiteralKind = LiteralKind.True;
                                CurrentToken =  Token.Literal;
                                return;

                            case "false":
                                LiteralKind = LiteralKind.False;
                                CurrentToken =  Token.Literal;
                                return;

                            case "null":
                                LiteralKind = LiteralKind.Null;
                                CurrentToken =  Token.Literal;
                                return;
                        }

                        CurrentToken =  Token.Identifier;
                        return;
                    }

                    // What the?
                    throw new InvalidDataException(string.Format("syntax error - unexpected character '{0}'", _currentChar));
                }
            }

            // Parse a sequence of characters that could make up a valid number
            // For performance, we don't actually parse it into a number yet.  When using PetaJsonEmit we parse
            // later, directly into a value type to avoid boxing
            private void TokenizeNumber()
            {
                _sb.Length = 0;

                // Leading negative sign
                bool signed = false;
                if (_currentChar == '-')
                {
                    signed = true;
                    _sb.Append(_currentChar);
                    NextChar();
                }

                // Hex prefix?
                bool hex = false;
                if (_currentChar == '0')
                {
                    _sb.Append(_currentChar);
                    NextChar();
                    if (_currentChar == 'x' || _currentChar == 'X')
                    {
                        _sb.Append(_currentChar);
                        NextChar();
                        hex = true;
                    }
                }

                // Process characters, but vaguely figure out what type it is
                bool cont = true;
                bool fp = false;
                while (cont)
                {
                    switch (_currentChar)
                    {
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                            _sb.Append(_currentChar);
                            NextChar();
                            break;

                        case 'A':
                        case 'a':
                        case 'B':
                        case 'b':
                        case 'C':
                        case 'c':
                        case 'D':
                        case 'd':
                        case 'F':
                        case 'f':
                            if (!hex)
                                cont = false;
                            else
                            {
                                _sb.Append(_currentChar);
                                NextChar();
                            }
                            break;

                        case '.':
                            if (hex)
                            {
                                cont = false;
                            }
                            else
                            {
                                fp = true;
                                _sb.Append(_currentChar);
                                NextChar();
                            }
                            break;

                        case 'E':
                        case 'e':
                            if (!hex)
                            {
                                fp = true;
                                _sb.Append(_currentChar);
                                NextChar();
                                if (_currentChar == '+' || _currentChar == '-')
                                {
                                    _sb.Append(_currentChar);
                                    NextChar();
                                }
                            }
                            break;

                        default:
                            cont = false;
                            break;
                    }
                }

                if (char.IsLetter(_currentChar))
                    throw new InvalidDataException(string.Format("syntax error - invalid character following number '{0}'", _sb.ToString()));

                // Setup token
                String = _sb.ToString();
                CurrentToken = Token.Literal;

                // Setup literal kind
                if (fp)
                {
                    LiteralKind = LiteralKind.FloatingPoint;
                }
                else if (signed)
                {
                    LiteralKind = LiteralKind.SignedInteger;
                }
                else
                {
                    LiteralKind = LiteralKind.UnsignedInteger;
                }
            }

            // Check the current token, throw exception if mismatch
            public void Check(Token tokenRequired)
            {
                if (tokenRequired != CurrentToken)
                {
                    throw new InvalidDataException(string.Format("syntax error - expected {0} found {1}", tokenRequired, CurrentToken));
                }
            }

            // Skip token which must match
            public void Skip(Token tokenRequired)
            {
                Check(tokenRequired);
                NextToken();
            }

            // Skip token if it matches
            public bool SkipIf(Token tokenRequired)
            {
                if (tokenRequired == CurrentToken)
                {
                    NextToken();
                    return true;
                }
                return false;
            }
        }
    }
}

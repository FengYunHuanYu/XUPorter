using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;


namespace UnityEditor.XCodeEditor
{
	public class PBXParser
	{
		public const string PBX_HEADER_TOKEN = "// !$*UTF8*$!\n";
		public const char WHITESPACE_SPACE = ' ';
		public const char WHITESPACE_TAB = '\t';
		public const char WHITESPACE_NEWLINE = '\n';
		public const char WHITESPACE_CARRIAGE_RETURN = '\r';
		public const char ARRAY_BEGIN_TOKEN = '(';
		public const char ARRAY_END_TOKEN = ')';
		public const char ARRAY_ITEM_DELIMITER_TOKEN = ',';
		public const char DICTIONARY_BEGIN_TOKEN = '{';
		public const char DICTIONARY_END_TOKEN = '}';
		public const char DICTIONARY_ASSIGN_TOKEN = '=';
		public const char DICTIONARY_ITEM_DELIMITER_TOKEN = ';';
		public const char QUOTEDSTRING_BEGIN_TOKEN = '"';
		public const char QUOTEDSTRING_END_TOKEN = '"';
		public const char QUOTEDSTRING_ESCAPE_TOKEN = '\\';
		public const char END_OF_FILE = (char)0x1A;
		public const string COMMENT_BEGIN_TOKEN = "/*";
		public const string COMMENT_END_TOKEN = "*/";
		public const string COMMENT_LINE_TOKEN = "//";
		private const int BUILDER_CAPACITY = 20000;

		private char[] data;
		private int index;

		public PBXDictionary Decode( string data )
		{
			if( !data.StartsWith( PBX_HEADER_TOKEN ) ) {
				Debug.Log( "Wrong file format." );
				return null;
			}

			data = data.Substring( 13 );
			this.data = data.ToCharArray();
			index = 0;

			return (PBXDictionary)ParseValue();
		}

		public string Encode( PBXDictionary pbxData, bool readable = false )
		{
			StringBuilder builder = new StringBuilder( PBX_HEADER_TOKEN, BUILDER_CAPACITY );
			bool success = SerializeValue( pbxData, builder, readable );

			return ( success ? builder.ToString() : null );
		}

		#region Pretty Print

		private void Indent( StringBuilder builder, int deep )
		{
			builder.Append( "".PadLeft( deep, '\t' ) );
		}

		private void Endline( StringBuilder builder, bool useSpace = false )
		{
			builder.Append( useSpace ? " " : "\n" );
		}

		private string marker = null;
		private void MarkSection(StringBuilder builder, string name)
		{
			if( marker == null && name == null ) return;

			if( marker != null && name != marker )
			{
				builder.Append( String.Format( "/* End {0} section */\n", marker ) );
			}

			if( name != null && name != marker )
			{
				builder.Append( String.Format( "\n/* Begin {0} section */\n", name ) );
			}

			marker = name;
		}

		#endregion

		#region Move

		private char NextToken()
		{
			SkipWhitespaces();
			return StepForeward();
		}

		private string Peek( int step = 1 )
		{
			string sneak = string.Empty;
			for( int i = 1; i <= step; i++ ) {
				if( data.Length - 1 < index + i ) {
					break;
				}
				sneak += data[ index + i ];
			}
			return sneak;
		}

		private bool SkipWhitespaces()
		{
			bool whitespace = false;
			while( Regex.IsMatch( StepForeward().ToString(), @"\s" ) )
				whitespace = true;

			StepBackward();

			if( SkipComments() ) {
				whitespace = true;
				SkipWhitespaces();
			}

			return whitespace;
		}

		private bool SkipComments()
		{
			string s = string.Empty;
			string tag = Peek( 2 );
			switch( tag ) {
				case COMMENT_BEGIN_TOKEN: {
						while( Peek( 2 ).CompareTo( COMMENT_END_TOKEN ) != 0 ) {
							s += StepForeward();
						}
						s += StepForeward( 2 );
						break;
					}
				case COMMENT_LINE_TOKEN: {
						while( !Regex.IsMatch( StepForeward().ToString(), @"\n" ) )
							continue;

						break;
					}
				default:
					return false;
			}
			return true;
		}

		private char StepForeward( int step = 1 )
		{
			index = Math.Min( data.Length, index + step );
			return data[ index ];
		}

		private char StepBackward( int step = 1 )
		{
			index = Math.Max( 0, index - step );
			return data[ index ];
		}

		#endregion
		#region Parse

		private object ParseValue()
		{
			switch( NextToken() ) {
				case END_OF_FILE:
					Debug.Log( "End of file" );
					return null;
				case DICTIONARY_BEGIN_TOKEN:
					return ParseDictionary();
				case ARRAY_BEGIN_TOKEN:
					return ParseArray();
				case QUOTEDSTRING_BEGIN_TOKEN:
					return ParseString();
				default:
					StepBackward();
					return ParseEntity();
			}
		}

		private PBXDictionary ParseDictionary()
		{
			SkipWhitespaces();
			PBXDictionary dictionary = new PBXDictionary();
			string keyString = string.Empty;
			object valueObject = null;

			bool complete = false;
			while( !complete ) {
				switch( NextToken() ) {
					case END_OF_FILE:
						Debug.Log( "Error: reached end of file inside a dictionary: " + index );
						complete = true;
						break;

					case DICTIONARY_ITEM_DELIMITER_TOKEN:
						keyString = string.Empty;
						valueObject = null;
						break;

					case DICTIONARY_END_TOKEN:
						keyString = string.Empty;
						valueObject = null;
						complete = true;
						break;

					case DICTIONARY_ASSIGN_TOKEN:
						valueObject = ParseValue();
						if (!dictionary.ContainsKey(keyString)) {
							dictionary.Add( keyString, valueObject );
						}
						break;

					default:
						StepBackward();
						keyString = ParseValue() as string;
						break;
				}
			}
			return dictionary;
		}

		private PBXList ParseArray()
		{
			PBXList list = new PBXList();
			bool complete = false;
			while( !complete ) {
				switch( NextToken() ) {
					case END_OF_FILE:
						Debug.Log( "Error: Reached end of file inside a list: " + list );
						complete = true;
						break;
					case ARRAY_END_TOKEN:
						complete = true;
						break;
					case ARRAY_ITEM_DELIMITER_TOKEN:
						break;
					default:
						StepBackward();
						list.Add( ParseValue() );
						break;
				}
			}
			return list;
		}

		private object ParseString()
		{
			string s = string.Empty;
			char c = StepForeward();
			while( c != QUOTEDSTRING_END_TOKEN ) {
				s += c;

				if( c == QUOTEDSTRING_ESCAPE_TOKEN )
					s += StepForeward();

				c = StepForeward();
			}

			return s;
		}

		private object ParseEntity()
		{
			string word = string.Empty;

			while( !Regex.IsMatch( Peek(), @"[;,\s=]" ) ) {
				word += StepForeward();
			}

			if( word.Length != 24 && Regex.IsMatch( word, @"^\d+$" ) ) {
				return Int32.Parse( word );
			}

			return word;
		}

		#endregion
		#region Serialize

		private bool SerializeValue( object value, StringBuilder builder, bool readable = false, int indent = 0 )
		{
			if( value == null ) {
				builder.Append( "null" );
			}
			else if( value is PBXObject ) {
				SerializeDictionary( ((PBXObject)value).data, builder, readable, indent );
			}
			else if( value is Dictionary<string, object> ) {
				SerializeDictionary( (Dictionary<string, object>)value, builder, readable, indent );
			}
			else if( value.GetType().IsArray ) {
				SerializeArray( new ArrayList( (ICollection)value ), builder, readable, indent );
			}
			else if( value is ArrayList ) {
				SerializeArray( (ArrayList)value, builder, readable, indent );
			}
			else if( value is string ) {
				SerializeString( (string)value, builder, readable );
			}
			else if( value is Char ) {
				SerializeString( Convert.ToString( (char)value ), builder, readable );
			}
			else if( value is bool ) {
				builder.Append( Convert.ToInt32( value ).ToString() );
			}
			else if( value.GetType().IsPrimitive ) {
				builder.Append( Convert.ToString( value ) );
			}
			else {
				Debug.LogWarning( "Error: unknown object of type " + value.GetType().Name );
				return false;
			}

			return true;
		}

		private bool SerializeDictionary( Dictionary<string, object> dictionary, StringBuilder builder, bool readable = false, int indent = 0 )
		{
			builder.Append( DICTIONARY_BEGIN_TOKEN );
			if( readable && dictionary.Count > 0 ) Endline( builder );

			foreach( KeyValuePair<string, object> pair in dictionary )
			{
				// output section banner if necessary
				if( readable && indent == 1 ) MarkSection( builder, pair.Value.GetType().Name );

				// indent KEY
				if( readable ) Indent( builder, indent + 1 );

				// KEY
				SerializeString( pair.Key, builder );

				// output file name
				if(readable && pair.Value is PBXBuildFile) {
					PBXFileReference fileRef = (PBXFileReference)dictionary[ ( (PBXBuildFile)pair.Value ).fileRef ];
					if( fileRef != null )
						builder.Append( String.Format( " /* {0} */", fileRef.name != null ? fileRef.name : fileRef.path ) );
				}

				// =
				if(readable)
					builder.Append( " " + DICTIONARY_ASSIGN_TOKEN + " " );
				else
					builder.Append( DICTIONARY_ASSIGN_TOKEN );

				// VALUE
				// do not pretty-print PBXBuildFile or PBXFileReference as Xcode does
				SerializeValue( pair.Value, builder, ( readable &&
					( pair.Value.GetType() != typeof( PBXBuildFile ) ) &&
					( pair.Value.GetType() != typeof( PBXFileReference ) )
				), indent + 1 );

				// end statement
				builder.Append( DICTIONARY_ITEM_DELIMITER_TOKEN );

				if( readable ) Endline( builder );
			}

			// output last section banner
			if( readable && indent == 1 ) MarkSection( builder, null );

			// indent }
			if( readable && dictionary.Count > 0 ) Indent( builder, indent );

			builder.Append( DICTIONARY_END_TOKEN );

			return true;
		}

		private bool SerializeArray( ArrayList anArray, StringBuilder builder, bool readable = false, int indent = 0 )
		{
			builder.Append( ARRAY_BEGIN_TOKEN );

			for( int i = 0; i < anArray.Count; i++ )
			{
				object value = anArray[i];

				if( !SerializeValue( value, builder, readable, indent + 1 ) )
				{
					return false;
				}

				builder.Append( ARRAY_ITEM_DELIMITER_TOKEN );
			}

			builder.Append( ARRAY_END_TOKEN );

			return true;
		}

		private bool SerializeString( string aString, StringBuilder builder, bool useQuotes = false, bool readable = false )
		{
			// Is a GUID?
			if( Regex.IsMatch( aString, @"^[A-F0-9]{24}$" ) ) {
				builder.Append( aString );
				return true;
			}

			// Is an empty string?
			if( string.IsNullOrEmpty( aString ) ) {
				builder.Append( QUOTEDSTRING_BEGIN_TOKEN );
				builder.Append( QUOTEDSTRING_END_TOKEN );
				return true;
			}

			if( !Regex.IsMatch( aString, @"^[A-Za-z0-9_.]+$" ) ) {
				useQuotes = true;
			}

			if( useQuotes )
				builder.Append( QUOTEDSTRING_BEGIN_TOKEN );

			builder.Append( aString );

			if( useQuotes )
				builder.Append( QUOTEDSTRING_END_TOKEN );

			return true;
		}

		#endregion
	}
}
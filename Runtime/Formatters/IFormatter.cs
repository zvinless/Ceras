﻿using System;
using Ceras.Helpers;

namespace Ceras.Formatters
{

	public interface IFormatter { }
	
	public interface IFormatter<T> : IFormatter
	{
		void Serialize(ref byte[] buffer, ref int offset, T value);
		void Deserialize(byte[] buffer, ref int offset, ref T value);
	}
	
	delegate void SerializeDelegate<T>(ref byte[] buffer, ref int offset, T value);
	delegate void DeserializeDelegate<T>(byte[] buffer, ref int offset, ref T value);
	
	delegate void StaticSerializeDelegate(ref byte[] buffer, ref int offset);
	delegate void StaticDeserializeDelegate(byte[] buffer, ref int offset);


	interface ISchemaTaintedFormatter
	{
		void OnSchemaChanged(TypeMetaData meta);
	}

	static class FormatterHelper
	{
		public static bool IsFormatterMatch(IFormatter formatter, Type type)
		{
			var closedFormatter = ReflectionHelper.FindClosedType(formatter.GetType(), typeof(IFormatter<>));
			
			var formattedType = closedFormatter.GetGenericArguments()[0];

			return type == formattedType;
		}

		public static void ThrowOnMismatch(IFormatter formatter, Type typeToFormat)
		{
			if(!IsFormatterMatch(formatter, typeToFormat))
				throw new InvalidOperationException($"The given formatter '{formatter.GetType().FullName}' is not an exact match for the formatted type '{typeToFormat.FullName}'");
		}
	}
}
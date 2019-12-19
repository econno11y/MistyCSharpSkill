using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MistyVoiceCommandSkill.DataObjects
{
	class OutputAudioEncodingConverter : JsonConverter
	{
			/// <summary>
			/// Called by NewtonSoft.Json's method to ask if this object can serialize
			/// an object of a given type.
			/// </summary>
			/// <returns>True if the objectType is a Protocol Message.</returns>
			public override bool CanConvert(Type objectType)
			{
				return typeof(OutputAudioEncoding).IsAssignableFrom(objectType);
			}

			/// <summary>
			/// Reads the json representation of a Protocol Message and reconstructs
			/// the Protocol Message.
			/// </summary>
			/// <param name="objectType">The Protocol Message type.</param>
			/// <returns>An instance of objectType.</returns>
			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
			{
				return OutputAudioEncoding.Linear16;
			}

			/// <summary>
			/// Writes the json representation of a Protocol Message.
			/// </summary>
			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				// Let Protobuf's JsonFormatter do all the work.
				writer.WriteRawValue(Google.Protobuf.JsonFormatter.Default
						.Format((IMessage)value));
			}
		}
	}

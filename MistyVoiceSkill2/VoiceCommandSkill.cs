using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf;
using Google.Protobuf.Collections;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;
using MistyRobotics.Tools.CommandFactory;
using MistyRobotics.Tools.DataStorage;
using MistyVoiceCommandSkill.DataObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Windows.Storage;

namespace SkillLibrary
{
	internal class VoiceCommandSkill : IMistySkill
	{
		/// <summary>
		/// Make a local variable to hold the misty robot interface, call it whatever you want 
		/// </summary>
		private IRobotMessenger _misty;
		private string _sessionId = "abc123";
		private string _projectId = "mistyapi-pxxkne";
		private Credential _credential;
		private HttpClient _client;
		private EventHandler<string> OnQueryResultReceived;


		/// <summary>
		/// Skill details for the robot
		/// 
		/// There are other parameters you can set if you want:
		///   Description - a description of your skill
		///   TimeoutInSeconds - timeout of skill in seconds
		///   StartupRules - a list of options to indicate if a skill should start immediately upon startup
		///   BroadcastMode - different modes can be set to share different levels of information from the robot using the "SkillData" websocket
		///   AllowedCleanupTimeInMs - How long to wait after calling OnCancel before denying messages from the skill and performing final cleanup  
		/// </summary>
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("VoiceCommandSkill", "c25c1f8a-55bf-4a05-b0c7-bac05087c63e")
		{
			TimeoutInSeconds = 60 * 30,   //runs for 30 minutes or until the skill is cancelled	
			AllowedCleanupTimeInMs = 6000,  // 6 seconds
			BroadcastMode = BroadcastMode.Verbose,
			
			StartupRules = new List<NativeStartupRule> { NativeStartupRule.Manual, NativeStartupRule.Startup }
		};

		/// <summary>
		///	This method is called by the wrapper to set your robot interface
		///	You need to save this off in the local variable commented on above as you are going use it to call the robot
		/// </summary>
		/// <param name="robotInterface"></param>
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
		}

		/// <summary>
		/// This event handler is called when the robot/user sends a start message
		/// The parameters can be set in the Skill Runner (or as json) and used in the skill if desired
		/// </summary>
		/// <param name="parameters"></param>
		public void OnStart(object sender, IDictionary<string, object> parameters)
		{
			_misty.ChangeLED(255, 255, 0, null);
			OnQueryResultReceived += ProcessQueryResult;
			_client = new HttpClient();
			_misty.RegisterBumpSensorEvent(onBumped, 20, true, null, "bumped", null);
			_misty.RegisterCapTouchEvent(onTouched, 20, true, null, "toched", null);

			try
			{
				_credential = getAuthToken()?.Result;
			}
			catch (Exception ex)
			{
				
				_misty.SkillLogger.Log("Unable to authenticate dialogflow client.", ex);
			}
		}

		private async Task<Credential> getAuthToken()
		{
			_misty.SendDebugMessage("Starting skill", null);
			HttpResponseMessage response = await _client.PostAsync("https://us-central1-mistyapi-pxxkne.cloudfunctions.net/authToken", null);
			string stringResponse = await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<Credential>(stringResponse);
		}

		private void onBumped(IBumpSensorEvent bumpEvent)
		{
			if (!bumpEvent.IsContacted)
			{
				_misty.RegisterVoiceRecordEvent(OnVoiceRecordReceived, 10, false, "VoiceRecordEvent", null);
				_misty.CaptureSpeech(false, true, 10000, 5000, null);
			}

		}

		private void onTouched(ICapTouchEvent capTouchEvent)
		{
			_misty.RegisterVoiceRecordEvent(OnVoiceRecordReceived, 10, false, "VoiceRecordEvent", null);
			
			_misty.StartKeyPhraseRecognition(true, true, 10000, 5000, null);
		}

		private void OnVoiceRecordReceived(IVoiceRecordEvent voiceEvent)
		{
			string fileName = "";
			if (voiceEvent.ErrorCode == 0)
			{
				fileName = voiceEvent.Filename;
				_misty.GetAudio(OnAudioReceived, fileName, true);
			}
		}


		private void OnAudioReceived(IRobotCommandResponse getAudioResponse)
		{
			AudioFile audioFile = ((IGetAudioResponse)getAudioResponse).Data;
			string base64 = audioFile?.Base64;

			_ = DetectIntent(base64);
		}




		private async Task<string> DetectIntent(string inputAudio)
		{

			InputAudioConfig audioConfig = ConfigureAudioInput();

			OutputAudioConfig outputConfig = ConfigureAudioOutput();

			QueryInput queryInput = new QueryInput()
			{
				AudioConfig = audioConfig,
			};

			DetectIntentContent detectIntentContent = new DetectIntentContent()
			{
				queryInput = queryInput,
				inputAudio = inputAudio,
				outputAudioConfig = outputConfig
			};

			JsonSerializerSettings settings = new JsonSerializerSettings();
			settings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();

			string content = JsonConvert.SerializeObject(detectIntentContent, settings);
			content = content.Replace(",\"inputCase\":1", "");

			Uri requestUri = new Uri("https://dialogflow.googleapis.com/v2/projects/mistyapi-pxxkne/agent/sessions/123456:detectIntent");
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri);
			request.Content = new StringContent(content, Encoding.UTF8);

			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _credential.AuthToken);
			HttpResponseMessage result = await _client.SendAsync(request);
			string stringResult = await result.Content.ReadAsStringAsync();


			OnQueryResultReceived?.Invoke(this, stringResult);
			return stringResult;
		}

		private InputAudioConfig ConfigureAudioInput()
		{
			AudioEncoding audioEncoding = AudioEncoding.Linear16;
			int sampleRateHertz = 16000;
			string languageCode = "en";

			return new InputAudioConfig()
			{
				SampleRateHertz = sampleRateHertz,
				AudioEncoding = audioEncoding,
				LanguageCode = languageCode
			};
		}

		private OutputAudioConfig ConfigureAudioOutput()
		{
			OutputAudioEncoding audioEncoding = OutputAudioEncoding.Linear16;
			SynthesizeSpeechConfig synthesizeSpeechConfig = new SynthesizeSpeechConfig()
			{
				SpeakingRate = 0.95,
				Pitch = 0,
				VolumeGainDb = 0,
				Voice = new VoiceSelectionParams()
				{
					Name = "en-US-Wavenet-F"
				}
			};

			return new OutputAudioConfig()
			{
				AudioEncoding = audioEncoding,
				SynthesizeSpeechConfig = synthesizeSpeechConfig,
				//SampleRateHertz = 44100

			};
		}



		private void ProcessQueryResult(object sender, string stringResult)
		{
			JObject dynamicResponse = JObject.Parse(stringResult);
			string color = ((string)dynamicResponse["queryResult"]?["parameters"]?["Color"])?.ToLower();
			string position = ((string)dynamicResponse["queryResult"]?["parameters"]?["Direction"])?.ToLower();
			string outputAudioString = (string)dynamicResponse["outputAudio"];
			byte[] outputAudio = Convert.FromBase64String(outputAudioString);

			try
			{
				JsonConverter[] converters = new JsonConverter[2] { new ProtoByteStringConverter(), new OutputAudioEncodingConverter() };
				DetectIntentResponse response = JsonConvert.DeserializeObject<DetectIntentResponse>(stringResult, converters);
				string intent = response?.QueryResult?.Intent?.DisplayName;

				string defaultResponse = response?.QueryResult?.FulfillmentText;

				switch (intent)
				{
					case "ChangeLED":
						if (color != null)
						{
							Color argbColor = Color.FromName(color);
							_misty.ChangeLED(argbColor.R, argbColor.G, argbColor.B, null);
						}
						break;
					case "Joke":
						_misty.PlayAudio(defaultResponse, 30, null);
						break;
					case "Move your arms":
						if (position == "up")
						{
							_misty.MoveArms(-90, -90, 50, 50, null, AngularUnit.Degrees, null);
						}

						if (position == "down")
						{
							_misty.MoveArms(90, 90, 50, 50, null, AngularUnit.Degrees, null);
						}
						_misty.Halt(null, null);

						break;

					case "MoveHeadPosition":
						if (position == "up")
						{
							_misty.MoveHead(-100, 0, 0, 50, AngularUnit.Degrees, null);
						}

						if (position == "down")
						{
							_misty.MoveHead(100, 0, 0, 50, AngularUnit.Degrees, null);
						}

						if (position == "right")
						{
							_misty.MoveHead(0, 0, -100, 50, AngularUnit.Degrees, null);
						}

						if (position == "left")
						{
							_misty.MoveHead(0, 0, 100, 50, AngularUnit.Degrees, null);
						}

						break;

					default:
						_misty.SaveAudio("tts.wav", outputAudio, true, true, null);
						break;
				}
			}
			catch (Exception ex)
			{

			}
		}

		private string GetRandomResponse(IList<Intent.Types.Message> responses)
		{
			Random rand = new Random();
			while (true)
			{
				int index = rand.Next(0, responses.Count);
				return responses[index]?.Text.ToString();
			}
		}

		/// <summary>
		/// This event handler is called when Pause is called on the skill
		/// User can save the skill status/data to be retrieved when Resume is called
		/// Infrastructure to help support this still under development, but users can implement this themselves as needed for now 
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//In this template, Pause is not implemented by default
		}

		/// <summary>
		/// This event handler is called when Resume is called on the skill
		/// User can restore any skill status/data and continue from Paused location
		/// Infrastructure to help support this still under development, but users can implement this themselves as needed for now 
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//TODO Put your code here and update the summary above
		}

		/// <summary>
		/// This event handler is called when the cancel command is issued from the robot/user
		/// You currently have a few seconds to do cleanup and robot resets before the skill is shut down... 
		/// Events will be unregistered for you 
		/// </summary>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			//TODO Put your code here and update the summary above
		}

		/// <summary>
		/// This event handler is called when the skill timeouts
		/// You currently have a few seconds to do cleanup and robot resets before the skill is shut down... 
		/// Events will be unregistered for you 
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			//TODO Put your code here and update the summary above
		}

		public void OnResponse(IRobotCommandResponse response)
		{
			Debug.WriteLine("Response: " + response.ResponseType.ToString());
		}

		#region IDisposable Support
		private bool _isDisposed = false;

		private void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				_isDisposed = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~MistyNativeSkill() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}

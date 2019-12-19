using MistyRobotics.Common.Types;
using MistyRobotics.SDK.Messengers;
using SkillLibrary;
using Windows.ApplicationModel.Background;

namespace MistyVoiceSkill2
{
		public sealed class StartupTask : IBackgroundTask
		{
				public void Run(IBackgroundTaskInstance taskInstance)
				{
						RobotMessenger.LoadAndPrepareSkill(taskInstance, new VoiceCommandSkill(), SkillLogLevel.Verbose);
				}
		}
}
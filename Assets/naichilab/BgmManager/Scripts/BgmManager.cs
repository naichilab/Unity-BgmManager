using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// BGMの再生、停止を制御します。
/// </summary>
public class BgmManager : MonoBehaviour
{

		#region Singleton

		private static BgmManager instance;

		public static BgmManager Instance {
				get {
						if (instance == null) {
								instance = (BgmManager)FindObjectOfType (typeof(BgmManager));

								if (instance == null) {
										Debug.LogError (typeof(BgmManager) + "is nothing");
								}
						}

						return instance;
				}
		}

		#endregion Singleton

		/// <summary>
		/// デバッグモード
		/// </summary>
		public bool DebugMode = true;
		/// <summary>
		/// BGM再生音量
		/// 次回フェードインから適用されます。
		/// 再生中の音量を変更するには、CurrentAudioSource.Volumeを変更してください。
		/// </summary>
		[Range (0f, 1f)]
		public float TargetVolume = 1.0f;
		/// <summary>
		/// フェードイン、フェードアウトにかかる時間です。
		/// </summary>
		public float TimeToFade = 2.0f;
		/// <summary>
		/// フェードインとフェードアウトの実行を重ねる割合です。
		/// 0を指定すると、完全にフェードアウトしてからフェードインを開始します。
		/// 1を指定すると、フェードアウトとフェードインを同時に開始します。
		/// </summary>
		[Range (0f, 1f)]
		public float CrossFadeRatio = 1.0f;
		/// <summary>
		/// 現在再生中のAudioSource
		/// FadeOut中のものは除く
		/// </summary>
		[NonSerialized]
		public AudioSource CurrentAudioSource = null;

		/// <summary>
		/// FadeOut中、もしくは再生待機中のAudioSource
		/// </summary>
		public AudioSource SubAudioSource {
				get { 
						//bgmSourcesのうち、CurrentAudioSourceでない方を返す
						if (this.AudioSources == null)
								return null;
						foreach (AudioSource s in this.AudioSources) {
								if (s != this.CurrentAudioSource) {
										return s;
								}
						}
						return null;
				}
		}

		/// <summary>
		/// BGMを再生するためのAudioSourceです。
		/// クロスフェードを実現するための２つの要素を持ちます。
		/// </summary>
		private List<AudioSource> AudioSources = null;
		/// <summary>
		/// 再生可能なBGM(AudioClip)のリストです。
		/// 実行時に Resources/Audio/BGM フォルダから自動読み込みされます。
		/// </summary>
		private Dictionary<string,AudioClip> AudioClipDict = null;
		/// <summary>
		/// コルーチン中断に使用
		/// </summary>
		private IEnumerator fadeOutCoroutine;
		/// <summary>
		/// コルーチン中断に使用
		/// </summary>
		private IEnumerator fadeInCoroutine;

		public void Awake ()
		{
				//シングルトンのためのコード
				if (this != Instance) {
						Destroy (this.gameObject);
						return;
				}
				DontDestroyOnLoad (this.gameObject);

				//AudioSourceを２つ用意。クロスフェード時に同時再生するために２つ用意する。
				this.AudioSources = new List<AudioSource> ();
				this.AudioSources.Add (this.gameObject.AddComponent<AudioSource> ());
				this.AudioSources.Add (this.gameObject.AddComponent<AudioSource> ());
				foreach (AudioSource s in this.AudioSources) {
						s.playOnAwake = false;
						s.volume = 0f;
						s.loop = true;
				}

				//[Resources/Audio/BGM]フォルダからBGMを探す
				this.AudioClipDict = new Dictionary<string, AudioClip> ();
				foreach (AudioClip bgm in Resources.LoadAll<AudioClip>("Audio/BGM")) {
						this.AudioClipDict.Add (bgm.name, bgm);
				}

				//有効なAudioListenerが一つも無い場合は生成する。（大体はMainCameraについてる）
				if (FindObjectsOfType (typeof(AudioListener)).All (o => !((AudioListener)o).enabled)) {
						this.gameObject.AddComponent<AudioListener> ();
				}
		}

		/// <summary>
		/// デバッグ用操作パネルを表示
		/// </summary>
		public void OnGUI ()
		{
				if (this.DebugMode) {
						//AudioClipが見つからなかった場合
						if (this.AudioClipDict.Count == 0) {
								GUI.Box (new Rect (10, 10, 200, 50), "BGM Manager(Debug Mode)");
								GUI.Label (new Rect (10, 35, 80, 20), "Audio clips not found.");
								return;
						}

						//枠
						GUI.Box (new Rect (10, 10, 200, 150 + this.AudioClipDict.Count * 25), "BGM Manager(Debug Mode)");
						int i = 0;
						GUI.Label (new Rect (20, 30 + i++ * 20, 180, 20), "Target Volume : " + this.TargetVolume.ToString ("0.00"));
						GUI.Label (new Rect (20, 30 + i++ * 20, 180, 20), "Time to Fade : " + this.TimeToFade.ToString ("0.00"));
						GUI.Label (new Rect (20, 30 + i++ * 20, 180, 20), "Crossfade Ratio : " + this.CrossFadeRatio.ToString ("0.00"));

						i = 0;
						//再生ボタン
						foreach (AudioClip bgm in this.AudioClipDict.Values) {
								bool currentBgm = (this.CurrentAudioSource != null && this.CurrentAudioSource.clip == this.AudioClipDict [bgm.name]);

								if (GUI.Button (new Rect (20, 100 + i * 25, 40, 20), "Play")) {
										this.Play (bgm.name);
								}
								string txt = string.Format ("[{0}] {1}", currentBgm ? "X" : "_", bgm.name);
								GUI.Label (new Rect (70, 100 + i * 25, 1000, 20), txt);

								i++;
						}

						//停止ボタン
						if (GUI.Button (new Rect (20, 100 + i++ * 25, 180, 20), "Stop")) {
								this.Stop ();
						}
						if (GUI.Button (new Rect (20, 100 + i++ * 25, 180, 20), "Stop Immediately")) {
								this.StopImmediately ();
						}
				}
		}

		/// <summary>
		/// BGMを再生します。
		/// </summary>
		/// <param name="bgmName">BGM名</param>
		public void Play (string bgmName)
		{
				if (!this.AudioClipDict.ContainsKey (bgmName)) {
						Debug.LogError (string.Format ("BGM名[{0}]が見つかりません。", bgmName));  
						return;
				}

				if ((this.CurrentAudioSource != null)
				    && (this.CurrentAudioSource.clip == this.AudioClipDict [bgmName])) {
						//すでに指定されたBGMを再生中
						return;
				}

				//クロスフェード中なら中止
				stopFadeOut ();
				stopFadeIn ();

				//再生中のBGMをフェードアウト開始
				this.Stop ();

				float fadeInStartDelay = this.TimeToFade * (1.0f - this.CrossFadeRatio);

				//BGM再生開始
				this.CurrentAudioSource = this.SubAudioSource;
				this.CurrentAudioSource.clip = this.AudioClipDict [bgmName];
				this.fadeInCoroutine = fadeIn (this.CurrentAudioSource, this.TimeToFade, this.CurrentAudioSource.volume, this.TargetVolume, fadeInStartDelay);
				StartCoroutine (this.fadeInCoroutine);
		}

		/// <summary>
		/// BGMを停止します。
		/// </summary>
		public void Stop ()
		{
				if (this.CurrentAudioSource != null) {
						this.fadeOutCoroutine = fadeOut (this.CurrentAudioSource, this.TimeToFade, this.CurrentAudioSource.volume, 0f);
						StartCoroutine (this.fadeOutCoroutine);
				}
		}

		/// <summary>
		/// BGMをただちに停止します。
		/// </summary>
		public void StopImmediately ()
		{
				this.fadeInCoroutine = null;
				this.fadeOutCoroutine = null;
				foreach (AudioSource s in this.AudioSources) {
						s.Stop ();
				}
				this.CurrentAudioSource = null;
		}

		/// <summary>
		/// BGMをフェードインさせながら再生を開始します。
		/// </summary>
		/// <param name="bgm">AudioSource</param>
		/// <param name="timeToFade">フェードインにかかる時間</param>
		/// <param name="fromVolume">初期音量</param>
		/// <param name="toVolume">フェードイン完了時の音量</param>
		/// <param name="delay">フェードイン開始までの待ち時間</param>
		private IEnumerator fadeIn (AudioSource bgm, float timeToFade, float fromVolume, float toVolume, float delay)
		{
				if (delay > 0) {
						yield return new WaitForSeconds (delay);
				}


				float startTime = Time.time;
				bgm.Play ();
				while (true) {
						float spentTime = Time.time - startTime;
						if (spentTime > timeToFade) {
								bgm.volume = toVolume;
								this.fadeInCoroutine = null;
								break;
						}

						float rate = spentTime / timeToFade;
						float vol = Mathf.Lerp (fromVolume, toVolume, rate);
						bgm.volume = vol;
						yield return null;
				}
		}

		/// <summary>
		/// BGMをフェードアウトし、その後停止します。
		/// </summary>
		/// <param name="bgm">フェードアウトさせるAudioSource</param>
		/// <param name="timeToFade">フェードアウトにかかる時間</param>
		/// <param name="fromVolume">フェードアウト開始前の音量</param>
		/// <param name="toVolume">フェードアウト完了時の音量</param>
		private IEnumerator fadeOut (AudioSource bgm, float timeToFade, float fromVolume, float toVolume)
		{ 
				float startTime = Time.time;
				while (true) {
						float spentTime = Time.time - startTime;
						if (spentTime > timeToFade) {
								bgm.volume = toVolume;
								bgm.Stop ();
								this.fadeOutCoroutine = null;
								break;
						}

						float rate = spentTime / timeToFade;
						float vol = Mathf.Lerp (fromVolume, toVolume, rate);
						bgm.volume = vol;
						yield return null;
				}
		}

		/// <summary>
		/// フェードイン処理を中断します。
		/// </summary>
		private void stopFadeIn ()
		{
				if (this.fadeInCoroutine != null)
						StopCoroutine (this.fadeInCoroutine);
				this.fadeInCoroutine = null;

		}

		/// <summary>
		/// フェードアウト処理を中断します。
		/// </summary>
		private void stopFadeOut ()
		{
				if (this.fadeOutCoroutine != null)
						StopCoroutine (this.fadeOutCoroutine);
				this.fadeOutCoroutine = null;
		}
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EnemyDialogueUI : MonoBehaviour
{
    [SerializeField] GameObject panelRoot;
    [SerializeField] TMP_Text speakerText;
    [SerializeField] TMP_Text lineText;
    [SerializeField] TMP_Text hintText;
    [SerializeField] Button advanceButton;
    [SerializeField] float charsPerSecond = 32f;
    [SerializeField] float inputLockSeconds = 0.25f;
    [SerializeField] float secondsAfterReveal;
    [SerializeField] bool keyboardAdvances = true;
    [SerializeField] string hintLabel = "Click to continue";

    bool _advanceRequested;
    bool _isPlaying;
    bool _inputUnlocked;
    float _unlockAtTime;
    AudioClip[] _talkSounds;
    float _talkSoundVolume = 1f;

    void Awake()
    {
        AutoWire();

        if (advanceButton != null)
            advanceButton.onClick.AddListener(OnAdvanceClicked);

        if (hintText != null)
            hintText.text = hintLabel;

        HideImmediate();
    }

    void OnDestroy()
    {
        if (advanceButton != null)
            advanceButton.onClick.RemoveListener(OnAdvanceClicked);
    }

    void AutoWire()
    {
        if (panelRoot == null)
        {
            Transform panel = transform.Find("DialoguePanel");
            if (panel != null)
                panelRoot = panel.gameObject;
        }

        if (speakerText == null)
            speakerText = FindText("DialoguePanel/SpeakerName");

        if (lineText == null)
            lineText = FindText("DialoguePanel/LineText");

        if (hintText == null)
            hintText = FindText("DialoguePanel/HintText");

        if (advanceButton == null && panelRoot != null)
            advanceButton = panelRoot.GetComponent<Button>();
    }

    TMP_Text FindText(string path)
    {
        Transform t = transform.Find(path);
        if (t == null)
            return null;

        return t.GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (!keyboardAdvances || !_isPlaying || !_inputUnlocked)
            return;

        if (WasKeyboardAdvancePressed())
            RequestAdvance();
    }

    static bool WasKeyboardAdvancePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (keyboard.spaceKey.wasPressedThisFrame)
            return true;

        if (keyboard.enterKey.wasPressedThisFrame)
            return true;

        return false;
    }

    public IEnumerator PlaySequence(string speakerName, string[] lines, AudioClip[] talkSounds = null, float talkSoundVolume = 1f)
    {
        AutoWire();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        _talkSounds = talkSounds;
        _talkSoundVolume = talkSoundVolume;
        if (_talkSoundVolume < 0f)
            _talkSoundVolume = 0f;

        _isPlaying = true;
        yield return PlaySequenceRoutine(speakerName, lines);
        _isPlaying = false;
        _inputUnlocked = false;
        _talkSounds = null;
    }

    IEnumerator PlaySequenceRoutine(string speakerName, string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            HideImmediate();
            yield break;
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (speakerText != null)
        {
            speakerText.text = speakerName ?? string.Empty;
            speakerText.ForceMeshUpdate();
        }

        bool showedAny = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            showedAny = true;
            yield return PlayLine(line);
        }

        if (!showedAny)
            Debug.LogWarning("EnemyDialogueUI: lines exist but all are empty.", this);

        HideImmediate();
    }

    IEnumerator PlayLine(string fullLine)
    {
        LockInput();
        _advanceRequested = false;

        float charsPerSec = charsPerSecond;
        if (charsPerSec < 1f)
            charsPerSec = 1f;

        int totalVisible = 0;
        if (lineText != null)
        {
            lineText.text = fullLine;
            lineText.maxVisibleCharacters = 0;
            lineText.ForceMeshUpdate();
            totalVisible = lineText.textInfo.characterCount;
        }

        int visibleCount = 0;
        float charTimer = 0f;

        while (visibleCount < totalVisible)
        {
            UnlockInputIfReady();

            if (_advanceRequested && _inputUnlocked)
            {
                RevealAllCharacters(totalVisible);
                _advanceRequested = false;
                break;
            }

            charTimer += Time.deltaTime * charsPerSec;
            int addCount = Mathf.FloorToInt(charTimer);
            if (addCount > 0)
            {
                charTimer -= addCount;
                int end = visibleCount + addCount;
                if (end > totalVisible)
                    end = totalVisible;

                for (int c = visibleCount; c < end; c++)
                {
                    if (IsVisibleNonWhitespace(c))
                        PlayTalkSound();
                }

                visibleCount = end;
                if (lineText != null)
                    lineText.maxVisibleCharacters = visibleCount;
            }

            yield return null;
        }

        RevealAllCharacters(totalVisible);

        _advanceRequested = false;
        LockInput();
        float waitElapsed = 0f;

        while (true)
        {
            UnlockInputIfReady();

            if (_advanceRequested && _inputUnlocked)
                break;

            if (secondsAfterReveal > 0f && waitElapsed >= secondsAfterReveal && _inputUnlocked)
                break;

            waitElapsed += Time.deltaTime;
            yield return null;
        }
    }

    void RevealAllCharacters(int totalVisible)
    {
        if (lineText == null)
            return;

        if (totalVisible <= 0)
            lineText.maxVisibleCharacters = int.MaxValue;
        else
            lineText.maxVisibleCharacters = totalVisible;
    }

    bool IsVisibleNonWhitespace(int visibleIndex)
    {
        if (lineText == null)
            return false;

        TMP_TextInfo info = lineText.textInfo;
        if (info == null || visibleIndex < 0 || visibleIndex >= info.characterCount)
            return false;

        char ch = info.characterInfo[visibleIndex].character;
        if (ch == 0)
            return false;

        return !char.IsWhiteSpace(ch);
    }

    void LockInput()
    {
        _inputUnlocked = false;
        _unlockAtTime = Time.unscaledTime + inputLockSeconds;
        _advanceRequested = false;
    }

    void UnlockInputIfReady()
    {
        if (_inputUnlocked)
            return;

        if (Time.unscaledTime >= _unlockAtTime)
            _inputUnlocked = true;
    }

    public void HideImmediate()
    {
        if (panelRoot != null && panelRoot != gameObject)
            panelRoot.SetActive(false);

        if (lineText != null)
        {
            lineText.text = string.Empty;
            lineText.maxVisibleCharacters = int.MaxValue;
        }
    }

    void OnAdvanceClicked()
    {
        if (!_isPlaying || !_inputUnlocked)
            return;

        RequestAdvance();
    }

    void RequestAdvance()
    {
        _advanceRequested = true;
    }

    void PlayTalkSound()
    {
        if (_talkSounds == null || _talkSounds.Length == 0)
            return;

        if (GameRoot.Instance == null || GameRoot.Instance.Audio == null)
            return;

        AudioClip clip = _talkSounds[Random.Range(0, _talkSounds.Length)];
        if (clip == null)
            return;

        GameRoot.Instance.Audio.PlaySfx(clip, _talkSoundVolume);
    }
}

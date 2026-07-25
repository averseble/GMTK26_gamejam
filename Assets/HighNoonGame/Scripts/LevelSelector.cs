using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class WantedEnemySlot
{
    public Transform cameraAnchor;
    public EnemyConfig enemyConfig;
    public GameObject posterRoot;
}

public class LevelSelector : MonoBehaviour
{
    [SerializeField] WantedBoardCamera boardCamera;
    [SerializeField] ScreenFader screenFader;
    [SerializeField] WantedEnemySlot[] slots;
    [SerializeField] int startIndex;
    [SerializeField] bool wrapSelection = true;
    [SerializeField] float fadeOutDuration = 0.4f;

    int _index;
    bool _confirmLocked;

    public int CurrentIndex
    {
        get { return _index; }
    }

    public EnemyConfig CurrentEnemy
    {
        get
        {
            if (!IsValidIndex(_index))
                return null;

            return slots[_index].enemyConfig;
        }
    }

    void Start()
    {
        if (boardCamera == null)
            boardCamera = FindFirstObjectByType<WantedBoardCamera>();

        if (screenFader == null && GameRoot.Instance != null)
            screenFader = GameRoot.Instance.Fader;

        if (screenFader == null)
            screenFader = FindFirstObjectByType<ScreenFader>();

        if (slots == null || slots.Length == 0)
        {
            Debug.LogError("LevelSelector: no wanted enemy slots assigned.");
            enabled = false;
            return;
        }

        ApplyUnlockVisibility();

        int preferred = GetInitialFocusIndex();
        _index = preferred;
        FocusCurrent(instant: true);
    }

    int GetInitialFocusIndex()
    {
        int preferred = startIndex;

        if (GameRoot.Instance != null && GameRoot.Instance.Run != null)
            preferred = GameRoot.Instance.Run.LastCompletedLevelIndex;

        preferred = Mathf.Clamp(preferred, 0, slots.Length - 1);

        if (!IsLevelUnlocked(preferred))
            preferred = GetMaxUnlockedIndex();

        if (preferred < 0)
            preferred = 0;

        return preferred;
    }

    void Update()
    {
        if (!CanAcceptInput())
            return;

        if (WasPressedLeft())
        {
            MoveSelection(-1);
            return;
        }

        if (WasPressedRight())
        {
            MoveSelection(1);
            return;
        }

        if (WasPressedConfirm())
            ConfirmSelection();
    }

    public void OnPreviousLevelClicked()
    {
        if (!CanAcceptInput())
            return;

        PlayClick();
        MoveSelection(-1);
    }

    public void OnNextLevelClicked()
    {
        if (!CanAcceptInput())
            return;

        PlayClick();
        MoveSelection(1);
    }

    public void OnConfirmLevelClicked()
    {
        if (!CanAcceptInput())
            return;

        PlayClick();
        ConfirmSelection();
    }

    public void OnBackToMenuClicked()
    {
        if (_confirmLocked)
            return;

        if (GameRoot.Instance == null || GameRoot.Instance.Run == null)
            return;

        PlayClick();
        _confirmLocked = true;
        GameRoot.Instance.Run.GoToMenu();
    }

    bool CanAcceptInput()
    {
        if (_confirmLocked)
            return false;

        if (boardCamera != null && boardCamera.IsBusy)
            return false;

        return true;
    }

    static void PlayClick()
    {
        if (GameRoot.Instance != null && GameRoot.Instance.Audio != null)
            GameRoot.Instance.Audio.PlayUiClick();
    }

    void ApplyUnlockVisibility()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            WantedEnemySlot slot = slots[i];
            if (slot == null)
                continue;

            bool unlocked = IsLevelUnlocked(i);
            if (slot.posterRoot != null)
                slot.posterRoot.SetActive(unlocked);
        }
    }

    void MoveSelection(int delta)
    {
        int next = FindUnlockedIndex(_index, delta);
        if (next < 0 || next == _index)
            return;

        _index = next;
        FocusCurrent(instant: false);
    }

    int FindUnlockedIndex(int from, int delta)
    {
        if (slots == null || slots.Length == 0)
            return -1;

        int maxUnlocked = GetMaxUnlockedIndex();
        if (maxUnlocked < 0)
            return -1;

        if (!wrapSelection)
        {
            int clamped = Mathf.Clamp(from + delta, 0, maxUnlocked);
            if (IsLevelUnlocked(clamped))
                return clamped;

            return from;
        }

        int count = maxUnlocked + 1;
        int local = from;
        for (int step = 0; step < count; step++)
        {
            local += delta;
            if (local < 0)
                local = maxUnlocked;
            else if (local > maxUnlocked)
                local = 0;

            if (IsLevelUnlocked(local))
                return local;
        }

        return from;
    }

    void FocusCurrent(bool instant)
    {
        if (boardCamera == null)
            return;

        if (!IsValidIndex(_index))
            return;

        Transform anchor = slots[_index].cameraAnchor;
        if (anchor == null)
        {
            Debug.LogWarning($"LevelSelector: cameraAnchor missing for slot {_index}");
            return;
        }

        boardCamera.Focus(anchor, instant);
    }

    void ConfirmSelection()
    {
        EnemyConfig enemy = CurrentEnemy;
        if (enemy == null)
        {
            Debug.LogError($"LevelSelector: EnemyConfig missing for slot {_index}");
            return;
        }

        if (!IsLevelUnlocked(_index))
            return;

        if (GameRoot.Instance == null || GameRoot.Instance.Run == null)
        {
            Debug.LogError("LevelSelector: GameRoot/RunManager missing.");
            return;
        }

        _confirmLocked = true;
        int selectedIndex = _index;
        int pending = 0;
        bool loaded = false;

        void TryLoadBattle()
        {
            pending--;
            if (pending > 0 || loaded)
                return;

            loaded = true;
            GameRoot.Instance.Run.SelectEnemyAndStartBattle(enemy, selectedIndex);
        }

        if (boardCamera != null)
        {
            pending++;
            boardCamera.PlayApproach(TryLoadBattle);
        }

        if (screenFader != null)
        {
            pending++;
            screenFader.FadeOut(fadeOutDuration, TryLoadBattle);
        }

        if (pending == 0)
            GameRoot.Instance.Run.SelectEnemyAndStartBattle(enemy, selectedIndex);
    }

    bool IsValidIndex(int index)
    {
        if (slots == null || slots.Length == 0)
            return false;

        if (index < 0 || index >= slots.Length)
            return false;

        return true;
    }

    bool IsLevelUnlocked(int levelIndex)
    {
        if (GameRoot.Instance == null || GameRoot.Instance.Run == null)
            return levelIndex == 0;

        return GameRoot.Instance.Run.IsLevelUnlocked(levelIndex);
    }

    int GetMaxUnlockedIndex()
    {
        if (slots == null || slots.Length == 0)
            return -1;

        int maxUnlocked = 0;
        if (GameRoot.Instance != null && GameRoot.Instance.Run != null)
            maxUnlocked = GameRoot.Instance.Run.MaxUnlockedIndex;

        if (maxUnlocked >= slots.Length)
            maxUnlocked = slots.Length - 1;

        return maxUnlocked;
    }

    static bool WasPressedLeft()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                return true;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    static bool WasPressedRight()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                return true;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    static bool WasPressedConfirm()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                return true;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame)
                return true;
        }

        return false;
    }
}

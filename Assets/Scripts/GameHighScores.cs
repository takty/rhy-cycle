using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameHighScores : MonoBehaviour
{
    private const string PlayerPrefsKey =
        "RhyCycle.HighScores";

    private const int MaximumEntryCount = 5;

    [Serializable]
    public sealed class Entry
    {
        public int Score;
        public int MaxAliveCount;
    }

    [Serializable]
    private sealed class SaveData
    {
        public List<Entry> Entries =
            new List<Entry>();
    }

    private SaveData data =
        new SaveData();

    public IReadOnlyList<Entry> Entries =>
        data.Entries;

    private void Awake()
    {
        Load();
    }

    public bool AddScore(
        int score,
        int maxAliveCount)
    {
        Entry newEntry = new Entry
        {
            Score = Mathf.Max(0, score),
            MaxAliveCount =
                Mathf.Max(0, maxAliveCount),
        };

        data.Entries.Add(newEntry);
        data.Entries.Sort(CompareEntries);

        if (data.Entries.Count >
            MaximumEntryCount)
        {
            data.Entries.RemoveRange(
                MaximumEntryCount,
                data.Entries.Count -
                MaximumEntryCount
            );
        }

        bool enteredRanking =
            data.Entries.Contains(newEntry);

        Save();

        Debug.Log(
            $"High score recorded: " +
            $"score={newEntry.Score}, " +
            $"maxAlive={newEntry.MaxAliveCount}, " +
            $"ranked={enteredRanking}"
        );

        return enteredRanking;
    }

    private static int CompareEntries(
        Entry first,
        Entry second)
    {
        int scoreComparison =
            second.Score.CompareTo(first.Score);

        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        return second.MaxAliveCount.CompareTo(
            first.MaxAliveCount
        );
    }

    private void Load()
    {
        string json =
            PlayerPrefs.GetString(
                PlayerPrefsKey,
                string.Empty
            );

        if (string.IsNullOrEmpty(json))
        {
            data = new SaveData();
            return;
        }

        try
        {
            SaveData loaded =
                JsonUtility.FromJson<SaveData>(
                    json
                );

            data =
                loaded != null &&
                loaded.Entries != null
                    ? loaded
                    : new SaveData();

            data.Entries.Sort(CompareEntries);

            if (data.Entries.Count >
                MaximumEntryCount)
            {
                data.Entries.RemoveRange(
                    MaximumEntryCount,
                    data.Entries.Count -
                    MaximumEntryCount
                );
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Failed to load high scores: " +
                exception.Message
            );

            data = new SaveData();
        }
    }

    private void Save()
    {
        string json =
            JsonUtility.ToJson(data);

        PlayerPrefs.SetString(
            PlayerPrefsKey,
            json
        );

        PlayerPrefs.Save();
    }

    [ContextMenu("Clear High Scores")]
    public void ClearScores()
    {
        data = new SaveData();

        PlayerPrefs.DeleteKey(
            PlayerPrefsKey
        );

        PlayerPrefs.Save();

        Debug.Log("High scores cleared.");
    }
}

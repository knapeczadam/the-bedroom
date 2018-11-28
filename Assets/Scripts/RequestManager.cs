using System;
using UnityEngine;
using System.Collections.Generic;
using PolyToolkit;

public class RequestManager : MonoBehaviour
{
    public Transform models;

    [Header("REQUEST OPTION")] 
    public string projectName;
    public bool useProjectName;
    public bool useID;

    [Space] 
    
    public PolyCategory category = PolyCategory.UNSPECIFIED;
    public bool curated = false;
    public PolyFormatFilter formatFilter = PolyFormatFilter.BLOCKS;
    public PolyMaxComplexityFilter maxComplexity = PolyMaxComplexityFilter.UNSPECIFIED;
    public PolyOrderBy orderBy = PolyOrderBy.BEST;
    public int pageSize = 20;

    private Dictionary<string, string> _namesIDs = new Dictionary<string, string>();
    private Dictionary<string, Queue<int>> _randomUniqueOrder = new Dictionary<string, Queue<int>>();
    private Dictionary<string, string> _keywordsModelNames = new Dictionary<string, string>();

    private void Start()
    {
        LoadSettings();

        foreach (Transform model in models)
        {
            string id = _namesIDs[model.name];
            if (id == null) continue;

            PolyListAssetsRequest req = CreateRequest(id, model);
            PolyApi.ListAssets(req, ListAssetsCallback);
        }
    }

    private void AppendUniqueNumberToModel(string id, Transform model)
    {
        int n = _randomUniqueOrder[id].Dequeue();
        model.name += " " + n;
    }

    private PolyListAssetsRequest CreateRequest(string id, Transform model)
    {
        PolyListAssetsRequest req = new PolyListAssetsRequest();

        AppendUniqueNumberToModel(id, model);

        req.keywords = GetKeywords(id, model);
        req.category = category;
        req.curated = curated;
        req.formatFilter = formatFilter;
        req.maxComplexity = maxComplexity;
        req.orderBy = orderBy;
        req.pageSize = pageSize;

        return req;
    }

    private string GetKeywords(string id, Transform model)
    {
        string keywords = model.name;

        if (useProjectName)
        {
            keywords = projectName.Trim() + " " + keywords;
        }

        if (useID)
        {
            keywords = keywords + " " + id;
        }

        _keywordsModelNames.Add(keywords, model.name);

        return keywords;
    }

    // Callback invoked when the featured assets results are returned.
    private void ListAssetsCallback(PolyStatusOr<PolyListAssetsResult> result)
    {
        if (!result.Ok)
        {
            Debug.LogError("Failed to get featured assets. :( Reason: " + result.Status);
            return;
        }

        Debug.Log("Successfully got featured assets!");
        // Set the import options.
        PolyImportOptions options = PolyImportOptions.Default();
        // We want to rescale the imported meshes to a specific size.
        options.rescalingMode = PolyImportOptions.RescalingMode.FIT;
        // The specific size we want assets rescaled to (fit in a 1x1x1 box):
        options.desiredSize = 1.0f;
        // We want the imported assets to be recentered such that their centroid coincides with the origin:
        options.recenter = true;

        // Now let's get the first asset and put it on the scene.
        List<PolyAsset> assetsInUse = new List<PolyAsset>();
        for (int i = 0; i < Mathf.Min(1, result.Value.assets.Count); i++)
        {
            // Import this asset.
            result.Value.assets[i].Keywords = result.Value.Keywords;
            PolyApi.Import(result.Value.assets[i], options, ImportAssetCallback);
            assetsInUse.Add(result.Value.assets[i]);
        }
    }

    // Callback invoked when an asset has just been imported.
    private void ImportAssetCallback(PolyAsset asset, PolyStatusOr<PolyImportResult> result)
    {
        if (!result.Ok)
        {
            Debug.LogError("Failed to import asset. :( Reason: " + result.Status);
            return;
        }

        string name = _keywordsModelNames[result.Value.Keywords];
        GameObject go = GameObject.Find(name);
        result.Value.gameObject.transform.position = go.transform.position;
        result.Value.gameObject.transform.localRotation = go.transform.localRotation;
        result.Value.gameObject.transform.localScale = go.transform.localScale;
    }

    private void LoadSettings()
    {
        TextAsset ta = Resources.Load("settings") as TextAsset;
        string[] lines = ta.text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            string[] line = lines[i].Split('|');

            string id = line[0];
            _namesIDs.Add(line[1], id);

            int maxRandomNumber = int.Parse(line[2]);
            Queue<int> randOrder = new Queue<int>();

            var seed = (int) DateTime.Now.Ticks;
            var rand = new System.Random(seed);
            while (randOrder.Count < maxRandomNumber)
            {
                int rInt = rand.Next(1, maxRandomNumber + 1);
                if (randOrder.Contains(rInt)) continue;
                randOrder.Enqueue(rInt);
            }

            _randomUniqueOrder.Add(id, randOrder);
        }
    }
}
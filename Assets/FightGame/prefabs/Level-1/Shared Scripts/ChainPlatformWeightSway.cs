using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Четыре пары «платформа + корень цепи»: при весе над коллайдером платформы слегка опускает
/// платформу и соответствующую группу цепей на одинаковую величину по локальному Y родителя.
/// После переименования объектов: задайте слоты вручную ИЛИ оставьте авто — 4 дочерних платформы с Collider2D
/// и 4 дочерних «цепи» сопоставятся по возрастанию localPosition.x.
/// </summary>
public class ChainPlatformWeightSway : MonoBehaviour
{
    [Tooltip("Корень «platforms» (родитель платформ). Если пусто — transform / поиск по имени platforms.")]
    [SerializeField] Transform platformsRoot;

    [Tooltip("Корень «chains». Если пусто — поиск по имени chains.")]
    [SerializeField] Transform chainsRoot;

    [Tooltip("Если здесь ровно 4 непустых ссылки — используются вместо имён и авто-по X (удобно после переименования).")]
    [SerializeField] Transform[] platformSlots;

    [Tooltip("Если ровно 4 непустых — пары с platformSlots по индексу; иначе с цепями по X вместе с платформами.")]
    [SerializeField] Transform[] chainSlots;

    [Tooltip("Смещение по локальному Y родителя (обычно отрицательное).")]
    [SerializeField] float dipLocalY = -0.075f;

    [SerializeField] float smoothTime = 0.14f;
    [Tooltip("Высота коробки проверки над верхом коллайдера платформы, мир.")]
    [SerializeField] float overlapProbeHeight = 0.14f;
    [Tooltip("Ширина проверки как доля ширины bounds платформы по X.")]
    [SerializeField] float overlapWidthMultiplier = 0.88f;

    struct Pair
    {
        public Transform platform;
        public Transform chainGroupRoot;
        public Collider2D platformCollider;
        public Vector3 initPlatformLocal;
        public Vector3 initChainLocal;
        public float currentDip;
        public float dipVelocity;
    }

    Pair[] pairs;

    static readonly string[][] PlatformNamesBySlot =
    {
        new[] { "platform 1", "platforma 1" },
        new[] { "platform 2", "platforma 1 (1)" },
        new[] { "platform 3", "platforma 1 (2)" },
        new[] { "platform 4", "platforma 1 (3)" },
    };

    static readonly string[] ChainNamesBySlot = { "chain 1", "chain 2", "chain 3", "chain 4" };

    static Transform FindDirectChildByName(Transform parent, string childName)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (string.Equals(c.name, childName, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    static Transform ResolveChainForSlot(Transform chainsParent, int slot)
    {
        if (chainsParent == null || slot < 0 || slot >= ChainNamesBySlot.Length)
            return null;

        Transform byName = FindDirectChildByName(chainsParent, ChainNamesBySlot[slot]);
        if (byName != null)
            return byName;

        if (slot < chainsParent.childCount)
            return chainsParent.GetChild(slot);

        return null;
    }

    static Transform ResolvePlatformsRoot(Transform self, Transform serializedRoot)
    {
        if (serializedRoot != null)
            return serializedRoot;
        if (string.Equals(self.name, "platforms", StringComparison.OrdinalIgnoreCase))
            return self;
        Transform walk = self;
        while (walk != null)
        {
            if (string.Equals(walk.name, "platforms", StringComparison.OrdinalIgnoreCase))
                return walk;
            walk = walk.parent;
        }

        GameObject found = GameObject.Find("platforms");
        return found != null ? found.transform : self;
    }

    static Transform ResolveChainsTransform(Transform serializedRoot)
    {
        if (serializedRoot != null)
            return serializedRoot;
        GameObject go = GameObject.Find("chains");
        if (go != null) return go.transform;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (string.Equals(root.name, "chains", StringComparison.OrdinalIgnoreCase))
                return root.transform;
        }

        return null;
    }

    Transform _chainsRootResolved;

    static bool SlotsComplete(Transform[] slots)
    {
        if (slots == null || slots.Length != 4) return false;
        for (int i = 0; i < 4; i++)
        {
            if (slots[i] == null) return false;
        }

        return true;
    }

    static List<Transform> CollectPlatformCandidates(Transform plRoot)
    {
        var list = new List<Transform>();
        if (plRoot == null) return list;
        for (int i = 0; i < plRoot.childCount; i++)
        {
            Transform t = plRoot.GetChild(i);
            if (t.GetComponent<Collider2D>() != null)
                list.Add(t);
        }

        list.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
        return list;
    }

    static List<Transform> CollectChainRoots(Transform chainsParent)
    {
        var list = new List<Transform>();
        if (chainsParent == null) return list;
        for (int i = 0; i < chainsParent.childCount; i++)
            list.Add(chainsParent.GetChild(i));
        list.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
        return list;
    }

    void Awake()
    {
        Transform plRoot = ResolvePlatformsRoot(transform, platformsRoot);
        Transform chains = ResolveChainsTransform(chainsRoot);

        if (chains == null)
        {
            Debug.LogError("[ChainPlatformWeightSway] Не задан chainsRoot и не найден объект «chains».");
            pairs = null;
            return;
        }

        _chainsRootResolved = chains;

        const int slotCount = 4;
        pairs = new Pair[slotCount];

        if (SlotsComplete(platformSlots) && SlotsComplete(chainSlots))
        {
            for (int slot = 0; slot < slotCount; slot++)
                AssignPair(slot, platformSlots[slot], chainSlots[slot]);
        }
        else
        {
            for (int slot = 0; slot < slotCount; slot++)
            {
                pairs[slot] = default;
                Transform pTr = null;
                foreach (string candidate in PlatformNamesBySlot[slot])
                {
                    pTr = FindDirectChildByName(plRoot, candidate);
                    if (pTr != null) break;
                }

                Transform cTr = ResolveChainForSlot(chains, slot);
                if (pTr != null && cTr != null)
                    AssignPair(slot, pTr, cTr);
            }

            if (!AllPairsValid())
                TryAutoPairBySortedX(plRoot, chains);
        }

        for (int slot = 0; slot < slotCount; slot++)
        {
            if (pairs[slot].platform == null || pairs[slot].chainGroupRoot == null)
                Debug.LogWarning($"[ChainPlatformWeightSway] Слот {slot + 1}/4 пуст: задайте platformSlots/chainSlots в инспекторе или проверьте 4 дочерних объекта с Collider2D под «{plRoot?.name}» и 4 под «{chains.name}».");
        }

        ValidateUniqueChains();
    }

    bool AllPairsValid()
    {
        if (pairs == null) return false;
        for (int i = 0; i < pairs.Length; i++)
        {
            if (pairs[i].platform == null || pairs[i].chainGroupRoot == null || pairs[i].platformCollider == null)
                return false;
        }

        return true;
    }

    void TryAutoPairBySortedX(Transform plRoot, Transform chainsTf)
    {
        List<Transform> ps = CollectPlatformCandidates(plRoot);
        List<Transform> cs = CollectChainRoots(chainsTf);
        if (ps.Count < 4 || cs.Count < 4)
        {
            Debug.LogWarning($"[ChainPlatformWeightSway] Авто-по X: нужно ≥4 платформ с Collider2D (есть {ps.Count}) и ≥4 дочерних цепей (есть {cs.Count}).");
            return;
        }

        for (int slot = 0; slot < 4; slot++)
            AssignPair(slot, ps[slot], cs[slot]);
    }

    void AssignPair(int slot, Transform pTr, Transform cTr)
    {
        if (pTr == null || cTr == null) return;
        Collider2D col = pTr.GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogWarning($"[ChainPlatformWeightSway] У «{pTr.name}» нет Collider2D (слот {slot + 1}).");
            return;
        }

        pairs[slot] = new Pair
        {
            platform = pTr,
            chainGroupRoot = cTr,
            platformCollider = col,
            initPlatformLocal = pTr.localPosition,
            initChainLocal = cTr.localPosition,
            currentDip = 0f,
            dipVelocity = 0f,
        };
    }

    void ValidateUniqueChains()
    {
        if (pairs == null) return;
        for (int i = 0; i < pairs.Length; i++)
        {
            if (pairs[i].chainGroupRoot == null) continue;
            for (int j = i + 1; j < pairs.Length; j++)
            {
                if (pairs[j].chainGroupRoot != null && pairs[i].chainGroupRoot == pairs[j].chainGroupRoot)
                    Debug.LogError($"[ChainPlatformWeightSway] Один корень цепи «{pairs[i].chainGroupRoot.name}» в слотах {i + 1} и {j + 1}.");
            }
        }
    }

    void LateUpdate()
    {
        if (pairs == null) return;

        for (int i = 0; i < pairs.Length; i++)
        {
            ref Pair p = ref pairs[i];
            if (p.platform == null || p.chainGroupRoot == null || p.platformCollider == null)
                continue;

            bool occupied = IsSomeoneOnPlatform(p.platformCollider, p.platform, _chainsRootResolved);
            float target = occupied ? dipLocalY : 0f;
            p.currentDip = Mathf.SmoothDamp(p.currentDip, target, ref p.dipVelocity, Mathf.Max(0.01f, smoothTime));
            Vector3 offset = Vector3.up * p.currentDip;
            p.platform.localPosition = p.initPlatformLocal + offset;
            p.chainGroupRoot.localPosition = p.initChainLocal + offset;
        }
    }

    bool IsSomeoneOnPlatform(Collider2D platformCol, Transform platformRoot, Transform allChainsRoot)
    {
        Bounds b = platformCol.bounds;
        float halfW = b.extents.x * overlapWidthMultiplier;
        float halfH = overlapProbeHeight * 0.5f;
        Vector2 center = new Vector2(b.center.x, b.max.y + halfH);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(halfW, halfH), 0f, -1, Mathf.NegativeInfinity, Mathf.Infinity);

        for (int h = 0; h < hits.Length; h++)
        {
            Collider2D c = hits[h];
            if (c == null || c.isTrigger) continue;
            if (c.transform == platformRoot || c.transform.IsChildOf(platformRoot)) continue;
            if (allChainsRoot != null && (c.transform == allChainsRoot || c.transform.IsChildOf(allChainsRoot))) continue;
            if (c.attachedRigidbody == null) continue;
            if (c.attachedRigidbody.bodyType == RigidbodyType2D.Static) continue;
            return true;
        }

        return false;
    }
}

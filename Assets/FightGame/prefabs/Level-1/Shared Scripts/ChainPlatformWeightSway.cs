using System;
using UnityEngine;

/// <summary>
/// Четыре пары «платформа + корень цепи»: при весе над коллайдером платформы слегка опускает
/// платформу и соответствующую группу цепей на одинаковую величину по локальному Y родителя.
/// </summary>
public class ChainPlatformWeightSway : MonoBehaviour
{
    [Tooltip("Корень «platforms» (родитель четырёх платформ). Если пусто — берётся transform или ищется объект platforms.")]
    [SerializeField] Transform platformsRoot;

    [Tooltip("Корень «chains» (родитель chain 1…4). Если пусто — ищется по имени chains.")]
    [SerializeField] Transform chainsRoot;

    [Tooltip("Смещение по локальному Y родителя (обычно отрицательное), одинаковое для платформы и цепи.")]
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

    /// <summary>Цепь по имени «chain N»; если не найдено — запасной вариант по индексу среди детей.</summary>
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
        if (self.name == "platforms")
            return self;
        Transform walk = self;
        while (walk != null)
        {
            if (walk.name == "platforms")
                return walk;
            walk = walk.parent;
        }

        GameObject found = GameObject.Find("platforms");
        return found != null ? found.transform : self;
    }

    Transform _chainsRootResolved;

    void Awake()
    {
        Transform plRoot = ResolvePlatformsRoot(transform, platformsRoot);
        Transform chains = chainsRoot;
        if (chains == null)
        {
            GameObject chainsGo = GameObject.Find("chains");
            chains = chainsGo != null ? chainsGo.transform : null;
        }

        if (chains == null)
        {
            Debug.LogError("[ChainPlatformWeightSway] Не задан chainsRoot и не найден объект «chains».");
            pairs = null;
            return;
        }

        _chainsRootResolved = chains;

        const int slotCount = 4;
        pairs = new Pair[slotCount];
        for (int slot = 0; slot < slotCount; slot++)
        {
            pairs[slot] = default;

            Transform pTr = null;
            foreach (string candidate in PlatformNamesBySlot[slot])
            {
                pTr = FindDirectChildByName(plRoot, candidate);
                if (pTr != null)
                    break;
            }

            Transform cTr = ResolveChainForSlot(chains, slot);
            if (pTr == null || cTr == null)
            {
                string tried = string.Join(" / ", PlatformNamesBySlot[slot]);
                Debug.LogWarning($"[ChainPlatformWeightSway] Пара {slot + 1}/4: не найдена платформа «{tried}» под «{plRoot.name}» или цепь «{ChainNamesBySlot[slot]}» под «{chains.name}».");
                continue;
            }

            Collider2D col = pTr.GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogWarning($"[ChainPlatformWeightSway] У платформы слота {slot + 1} («{pTr.name}») нет Collider2D.");
                continue;
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

        ValidateUniqueChains();
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
                    Debug.LogError($"[ChainPlatformWeightSway] Один и тот же корень цепи «{pairs[i].chainGroupRoot.name}» привязан к слотам {i + 1} и {j + 1} — движение будет перезаписываться.");
            }
        }
    }

    void FixedUpdate()
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace BaroqueUI
{
    public class ConsoleViewer : MonoBehaviour
    {
        public Sprite logSprite, warningSprite, errorSprite;

        List<RectTransform> items;
        int total_entries;

        void Awake()
        {
            items = new List<RectTransform>();
            Application.logMessageReceived += HandleLog;
        }

#if false
        /* Demo */
        private void FixedUpdate()
        {
            if (Random.Range(0, 100) == 50)
                Debug.Log("foo regular\nbar baz");
            if (Random.Range(0, 100) == 50)
                Debug.LogError("foo erorr\nbar baz");
            if (Random.Range(0, 100) == 50)
                Debug.LogWarning("foo warn\nbar baz");
        }
#endif

        void HandleLog(string logString, string stackTrace, LogType type)
        {
            RectTransform itemPrefab = transform.Find("Item Prefab") as RectTransform;
            RectTransform viewport = transform.Find("Viewport") as RectTransform;
            RectTransform item = Instantiate<RectTransform>(itemPrefab, viewport);

            if ((total_entries++ & 1) == 0)
                Destroy(item.GetComponent<Image>());

            Sprite spr;
            switch (type)
            {
                case LogType.Log: spr = logSprite; break;
                case LogType.Warning: spr = warningSprite; break;
                default: spr = errorSprite; break;
            }
            item.Find("Image").GetComponent<Image>().sprite = spr;
            item.Find("Text").GetComponent<Text>().text = logString;
            items.Add(item as RectTransform);
            item.gameObject.SetActive(true);

            /* recompute the vertical positions */
            float max_y = viewport.rect.height;
            float item_y = item.rect.height;

            int max_items = Mathf.CeilToInt(max_y / item_y);
            while (items.Count > max_items)
            {
                Destroy(items[0].gameObject);
                items.RemoveAt(0);
            }

            float y0 = item_y * items.Count;
            if (y0 < max_y - item_y * 0.25f)
                y0 = max_y - item_y * 0.25f;

            foreach (var rtr in items)
            {
                rtr.offsetMin = new Vector2(0, y0 - item_y);
                rtr.offsetMax = new Vector2(0, y0);
                y0 -= item_y;
            }
        }
    }
}
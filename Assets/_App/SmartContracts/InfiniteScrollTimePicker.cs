using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class InfiniteScrollTimePicker : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private int visibleCount = 7; // Must be odd (e.g. 7)
    [SerializeField] private float itemHeight = 90f;
    [SerializeField] private bool isHourPicker = true; // true = hours, false = minutes

    private readonly List<ScrollItem> items = new();
    private int centerIndex;
    private int selectedValue = 0;

    private void Start()
    {
        InitItems();
    }

    private void Update()
    {
        HandleLooping();
        UpdateVisuals();
    }

    private void InitItems()
    {
        centerIndex = visibleCount / 2;

        int poolSize = visibleCount + 2; // 2 extra for smooth wrapping
        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = Instantiate(itemPrefab, content);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, -i * itemHeight);

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            int value = GetLoopValue(i - centerIndex);
            label.text = value.ToString("D2");

            items.Add(new ScrollItem { rect = rt, label = label, value = value });
        }
        
        // 👇 Ensure the content is large enough to scroll
        var height = (poolSize + 10) * itemHeight;
        content.sizeDelta = new Vector2(content.sizeDelta.x, height);
    }

    private void HandleLooping()
    {
        float totalHeight = items.Count * itemHeight;
        float halfRange = totalHeight / 2f;

        foreach (var item in items)
        {
            float anchoredY = item.rect.anchoredPosition.y;

            // Recycle upward (too far down visually)
            if (anchoredY > halfRange)
            {
                item.rect.anchoredPosition -= new Vector2(0, totalHeight);
                item.value = GetLoopValue(item.value - items.Count);
                item.label.text = item.value.ToString("D2");
            }
            // Recycle downward (too far up visually)
            else if (anchoredY < -halfRange)
            {
                item.rect.anchoredPosition += new Vector2(0, totalHeight);
                item.value = GetLoopValue(item.value + items.Count);
                item.label.text = item.value.ToString("D2");
            }
        }
    }
    
   private void UpdateVisuals()
   {
       float closestDistance = float.MaxValue;
       int closestValue = selectedValue;
   
       foreach (var item in items)
       {
           float distance = Mathf.Abs(item.rect.position.y - viewport.position.y);
           float t = Mathf.Clamp01(1 - (distance / (itemHeight * 2f)));
           float scale = Mathf.Lerp(0.5f, 1f, t);
           float alpha = Mathf.Lerp(0.2f, 1f, t);
   
           item.rect.localScale = Vector3.one * scale;
           var color = item.label.color;
           item.label.color = new Color(color.r, color.g, color.b, alpha);
   
           // Track the item closest to the center
           if (distance < closestDistance)
           {
               closestDistance = distance;
               closestValue = item.value;
           }
       }
   
       selectedValue = closestValue;
   }

   private int GetLoopValue(int index)
   {
       int max = isHourPicker ? 24 : 60;
       return (index % max + max) % max;
   }

    
    public void ScrollToValue(int targetValue)
    {
        int max = isHourPicker ? 24 : 60;

        // Clamp within range
        targetValue = Mathf.Clamp(targetValue, 0, max - 1);

        // Update all items so the center one reflects targetValue
        int centerIndex = visibleCount / 2;

        for (int i = 0; i < items.Count; i++)
        {
            int offset = i - centerIndex;
            int displayValue = GetLoopValue(targetValue + offset);
            items[i].value = displayValue;
            items[i].label.text = displayValue.ToString("D2");
            items[i].rect.anchoredPosition = new Vector2(0, -offset * itemHeight);
        }

        // Update internal state
        selectedValue = targetValue;
        UpdateVisuals();
    }

    
    public int GetSelectedValue() => selectedValue;


    private class ScrollItem
    {
        public RectTransform rect;
        public TextMeshProUGUI label;
        public int value;
    }
}

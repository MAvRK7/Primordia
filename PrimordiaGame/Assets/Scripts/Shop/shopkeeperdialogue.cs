using System.Collections;
using TMPro;
using UnityEngine;

// Drives a simple world-space speech bubble above the shopkeeper's head.
// Attach to the shopkeeper, assign a TMP text + CanvasGroup for the bubble.
public class ShopkeeperDialogue : MonoBehaviour
{
    [Header("Speech Bubble")]
    public TMP_Text speechText;
    public CanvasGroup speechBubble;
    public float lineDisplayTime = 3f;
    public float fadeSpeed = 4f;

    [Header("Lines")]
    public string[] greetingLines =
    {
        "What are you buying today?",
        "Got some fresh gear if you've got the parts.",
        "Meat and bones, that's all I need."
    };

    public string[] purchaseSuccessLines =
    {
        "Pleasure doing business.",
        "Good choice, hunter.",
        "Use it well out there."
    };

    public string[] purchaseFailLines =
    {
        "You're short on parts, come back later.",
        "Not enough meat or bone for that one."
    };

    private Coroutine activeRoutine;

    public void SayGreeting() => Say(RandomLine(greetingLines));
    public void SayPurchaseSuccess() => Say(RandomLine(purchaseSuccessLines));
    public void SayPurchaseFail() => Say(RandomLine(purchaseFailLines));

    private string RandomLine(string[] lines)
    {
        if (lines == null || lines.Length == 0) return string.Empty;
        return lines[Random.Range(0, lines.Length)];
    }

    private void Say(string line)
    {
        if (string.IsNullOrEmpty(line) || speechText == null) return;

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(ShowLine(line));
    }

    private IEnumerator ShowLine(string line)
    {
        speechText.text = line;

        yield return Fade(1f);
        yield return new WaitForSeconds(lineDisplayTime);
        yield return Fade(0f);
    }

    private IEnumerator Fade(float target)
    {
        if (speechBubble == null) yield break;

        while (!Mathf.Approximately(speechBubble.alpha, target))
        {
            speechBubble.alpha = Mathf.MoveTowards(speechBubble.alpha, target, fadeSpeed * Time.deltaTime);
            yield return null;
        }
    }
}
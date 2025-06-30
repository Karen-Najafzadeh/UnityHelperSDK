using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class TutorialUIController : MonoBehaviour
{
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] Image iconImage;

    void OnEnable()
    {
        TutorialManager.Instance.OnStepEntered += ShowStep;
        TutorialManager.Instance.OnTutorialCompleted += HideAll;
    }

    void OnDisable()
    {
        TutorialManager.Instance.OnStepEntered -= ShowStep;
        TutorialManager.Instance.OnTutorialCompleted -= HideAll;
    }

    void ShowStep(StepDefinitionSO step)
    {
        titleText.text = step.Title;
        bodyText.text = step.Body;
        iconImage.sprite = step.Icon;
        gameObject.SetActive(true);
    }

    void HideAll()
    {
        gameObject.SetActive(false);
    }
}

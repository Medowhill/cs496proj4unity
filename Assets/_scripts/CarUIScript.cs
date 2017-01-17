using UnityEngine;
using UnityEngine.UI;

public class CarUIScript : MonoBehaviour
{
    public delegate void Trigger(Collider other);

    public GameObject m_SpeedoMeterPointer;
    public Text m_TimeText, m_CountText, m_InfoText, m_RankText;
    public CanvasGroup m_Image, m_HideImage;
    public Image m_ItemImage;
    public Sprite m_BoostSprite, m_BulletSprite, m_ShieldSprite;

    private const float m_MaxSpeed = 200;
    private float m_CurrentSpeed = 0;
    private string m_Text = "", m_TextInfo = "", m_TextTime = "", m_TextRank = "";
    private bool m_UTurn = false, m_Hide = false;
    private int m_ItemNum = -1;
    private Trigger m_TriggerExit, m_TriggerEnter;

    public void Start()
    {
    }

    private void OnTriggerEnter(Collider other)
    {
        m_TriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        m_TriggerExit(other);
    }

    private void Update()
    {
        float factor = m_CurrentSpeed / m_MaxSpeed;
        float angle;
        if (m_CurrentSpeed >= 0)
            angle = Mathf.Lerp(0, 180, factor);
        else
            angle = Mathf.Lerp(0, 180, -factor);
        m_SpeedoMeterPointer.transform.Rotate(new Vector3(0, 0, -angle - m_SpeedoMeterPointer.transform.rotation.eulerAngles.z));

        m_TimeText.text = m_TextTime;
        m_CountText.text = m_Text;
        m_InfoText.text = m_TextInfo;
        m_RankText.text = m_TextRank;
        m_Image.alpha = m_UTurn ? 1 : 0;
        m_HideImage.alpha = m_Hide ? 1 : 0;
        switch (m_ItemNum) {
            case -1:
                m_ItemImage.sprite = null;
                break;
            case 0:
                m_ItemImage.sprite = m_BoostSprite;
                break;
            case 1:
                m_ItemImage.sprite = m_BulletSprite;
                break;
            case 2:
                m_ItemImage.sprite = m_ShieldSprite;
                break;
        }
    }

    public void setSpeed(float currentSpeed)
    {
        m_CurrentSpeed = currentSpeed;
    }

    public void setText(string text)
    {
        m_Text = text;
    }

    public void setTimeText(string text)
    {
        m_TextTime = text;
    }

    public void setInfoText(string text)
    {
        m_TextInfo = text;
    }

    public void setTriggerEnter(Trigger triggerEnter) {
        m_TriggerEnter = triggerEnter;
    }

    public void setTriggerExit(Trigger triggerExit)
    {
        m_TriggerExit = triggerExit;
    }

    public void setUTurn(bool uTurn)
    {
        m_UTurn = uTurn;
    }

    public void setItem(int item)
    {
        m_ItemNum = item;
    }

    public void setHide(bool hide) {
        m_Hide = hide;
    }

    public void setRank(string rank)
    {
        m_TextRank = rank;
    }
}
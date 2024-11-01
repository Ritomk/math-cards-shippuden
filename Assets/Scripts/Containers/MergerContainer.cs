using System;
using System.Collections;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class MergerContainer : CardContainerBase
{
    [SerializeField] private SoAnimationEvents soAnimationEvents;
    [SerializeField] private SoCardEvents soCardEvents;
    [SerializeField] private SoContainerEvents soContainerEvents;
    [SerializeField] private ChestAnimation chestAnimation;
    [SerializeField] private Transform cardPositionTransform;

    private Vector3 _baseCardRotation = new Vector3(90, 0, 90);

    private void Awake()
    {
        Application.targetFrameRate = 0;
    }

    private void OnEnable()
    {
        soAnimationEvents.OnToggleChestAnimation += HandleToggleCardsVisibility;
        soContainerEvents.OnMergeCards += HandleMergeCards;
        soContainerEvents.OnChangeCardsState += HandleChangeCardsState;
        soContainerEvents.OnValidateCardPlacement += ValidateCardPlacement;
    }


    private void OnDisable()
    {
        soAnimationEvents.OnToggleChestAnimation -= HandleToggleCardsVisibility;
        soContainerEvents.OnMergeCards -= HandleMergeCards;
        soContainerEvents.OnChangeCardsState -= HandleChangeCardsState;
        soContainerEvents.OnValidateCardPlacement -= ValidateCardPlacement;
    }

    public override bool AddCard(Card card)
    {
        bool result = base.AddCard(card);
        if (result)
        {
            UpdateCardPositions();
        }
        
        return result;
    }

    protected override void ValidateCardPlacement()
    {
        var lastCard = cardsDictionary.LastOrDefault().Value;
        if (lastCard != null && lastCard.TokenType != CardData.TokenType.SingleDigit)
        {
            BurnCard(lastCard.CardId);
        }
    }

    private void UpdateCardPositions()
    {
        int cardCount = cardsDictionary.Count;
        
        if (cardCount == 1)
        {
            var singleCard = cardsDictionary.Values.FirstOrDefault();
            if (singleCard != null)
            {
                singleCard.transform.localPosition = cardPositionTransform.localPosition;
                singleCard.transform.localRotation = cardPositionTransform.localRotation;
            }
        }
        else if (cardCount == 2)
        {
            var firstCard = cardsDictionary.Values.ElementAt(0);
            var secondCard = cardsDictionary.Values.ElementAt(1);

            var baseRotation = cardPositionTransform.localRotation;
            
            firstCard.transform.localPosition = cardPositionTransform.localPosition + new Vector3(0, 0, 0.75f);
            firstCard.transform.localRotation = baseRotation * Quaternion.Euler(0, -10, 0);
    
            secondCard.transform.localPosition = cardPositionTransform.localPosition + new Vector3(0.1f, 0, -0.75f);
            secondCard.transform.localRotation = baseRotation * Quaternion.Euler(0, 10, 0);
        }
    }

    private void PositionCard(Card card, Vector3 position, Quaternion rotation)
    {
        if (card == null) return;
        card.transform.position = position;
        card.transform.rotation = rotation;
    }

    private void HandleToggleCardsVisibility(bool isVisible)
    {
        cardsDictionary.Values
            .Where(card => card != null)
            .ToList()
            .ForEach(card => card.gameObject.SetActive(isVisible));
    }

    private void HandleMergeCards()
    {
        CoroutineHelper.Start(MergeCards());
    }
    
    private void HandleChangeCardsState(CardData.CardState newState)
    {
        foreach (var card in cardsDictionary.Values)
        {
            card.State = newState;
        }
    }

    private IEnumerator MergeCards()
    {
        if(cardsDictionary.Count != 2) yield break;

        soAnimationEvents.RaiseToggleChestAnimation(false);
        yield return new WaitWhile(() => chestAnimation!.IsMoving);
        
        var firstCard = cardsDictionary.Values.ElementAt(0);
        var secondCard = cardsDictionary.Values.ElementAt(1);
        
        firstCard.State = CardData.CardState.Normal;
        firstCard.Token += secondCard.Token;
        BurnCard(secondCard.CardId);
        
        UpdateCardPositions();
        
        soAnimationEvents.RaiseToggleChestAnimation(true);
        yield return new WaitWhile(() => chestAnimation!.IsMoving);

        soCardEvents.RaiseCardMove(firstCard, ContainerType, CardContainerType.Hand);
    }
}
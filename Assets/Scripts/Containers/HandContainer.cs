using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

public class HandContainer : CardContainerBase, IDrawableContainer
{
    [Header("Hand Container Settings")]
    [SerializeField] private SoGameStateEvents soGameStateEvents;
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private float initialSpacing = 1f;
    [SerializeField] private Transform lookAtObject;
    private Vector3 _centerPosition;
    
    private System.Random _random = new System.Random();
    
    private void Start()
    {
        Vector3 firstPoint = splineContainer.transform.TransformPoint(splineContainer.Spline[0].Position);
        Vector3 lastPoint = splineContainer.transform.TransformPoint(splineContainer.Spline[^1].Position);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        soContainerEvents.OnValidateCardPlacement += ValidateCards;
        soContainerEvents.OnBurnMerged += HandleBurnMerged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        
        soContainerEvents.OnValidateCardPlacement -= ValidateCards;
        soContainerEvents.OnBurnMerged += HandleBurnMerged;
    }

    private void ValidateCards()
    {
        if (SelfContainerKey.OwnerType is OwnerType.Player &&
            soGameStateEvents.CurrentPlayerState is PlayerStateEnum.CardPlacedMerger
                or PlayerStateEnum.PlayerTurnIdle)
        {
            var groupedCards = CardsDictionary.Values
                .GroupBy(card => card.Token)
                .Select(group => new
                {
                    Token = group.Key,
                    ActiveCard = group.FirstOrDefault(card => card.gameObject.activeSelf),
                    TotalWeight = group.Sum(card => card.TokenWeight)
                })
                .Where(group => group.ActiveCard)
                .OrderBy(group => group.Token)
                .ToList();
            
            var totalWeight = groupedCards.Sum(card => card.TotalWeight);
            
            var randomValue = _random.Next(totalWeight);
            var cumulativeWeight = 0;
            
            var selectedCard = groupedCards
                .FirstOrDefault(group =>
                {
                    cumulativeWeight += group.TotalWeight;
                    return randomValue < cumulativeWeight;
                })
                ?.ActiveCard;
            
            if (selectedCard)
            {
                CoroutineHelper.Start(BurnCard(selectedCard.CardId));
            }
            else
            {
                Debug.LogError($"Selected card: None");
                Debug.LogError($"Selected card total weight: {totalWeight}, cumulative weight: {randomValue}");
            }
        }
    }

    public Card DrawCard()
    {
        if (CardsDictionary.Count == 0)
        {
            Debug.LogWarning("No cards left to draw.");
            return null;
        }

        var card = CardsDictionary.Last();
        RemoveCard(card.Key);
        card.Value.State = CardData.CardState.Normal;
        return card.Value;
    }

    public override IEnumerator BurnCard(int cardId)
    {
        yield return base.BurnCard(cardId);
        UpdateCardPositions();
    }

    public override bool AddCard(Card card)
    {
        bool result = base.AddCard(card);
        if (result)
        {
            card.transform.position = _centerPosition;
            UpdateCardPositions();
            
            if (SelfContainerKey.OwnerType == OwnerType.Player)
            {
                var existingActiveCard = CardsDictionary.Values
                    .FirstOrDefault(c => c.Token == card.Token && c.gameObject.activeSelf && c != card);

                if (existingActiveCard != null)
                {
                    existingActiveCard.AddCardToHand();
                }
                else
                {
                    card.AddCardToHand();
                }
            }
            else if (SelfContainerKey.OwnerType == OwnerType.Enemy)
            {
                card.State = CardData.CardState.EnemyHand;
            }
        }
        
        return result;
    }

    public override bool RemoveCard(int cardId)
    {
        CardsDictionary.TryGetValue(cardId, out Card card);
        
        bool result = base.RemoveCard(cardId);
        
        if (result)
        {
            
            UpdateCardPositions();

            if (card)
            {
                card.gameObject.SetActive(true);
                card.Duplicates = 0;
                
                if (SelfContainerKey.OwnerType == OwnerType.Enemy)
                {
                    card.State = CardData.CardState.Placed;
                }
            }
        }
        
        return result;
    }


    private void UpdateCardPositions()
    {
        if (CardsDictionary.Count == 0) return;

        var groupedCards = CardsDictionary.Values
            .GroupBy(card => card.Token)
            .Select(group => new
            {
                Token = group.Key,
                Card = group.First(),
                Count = group.Count()
            })
            .OrderBy(card => card.Card.TokenType)
            .ThenBy(card => card.Token)
            .ToList();
        
        var activeCardSet = new HashSet<Card>(groupedCards.Select(card => card.Card));
        
        foreach (var cardEntry in CardsDictionary.Values)
        {
            cardEntry.gameObject.SetActive(activeCardSet.Contains(cardEntry));
        }

        int count = groupedCards.Count;
        
        float totalSpacing = initialSpacing * (count - 1);
        float splineLength = splineContainer.Spline.GetLength();
        float scaleFactor = (totalSpacing > splineLength) ? (splineLength / totalSpacing) : 1f;

        int i = 0;
        foreach(var cardEntry in groupedCards)
        {
            if(!cardEntry.Card.gameObject.activeSelf) cardEntry.Card.gameObject.SetActive(true);

            cardEntry.Card.Duplicates = cardEntry.Count;
            
            Transform cardTransform = cardEntry.Card.transform;
            float offset = i - (count - 1) / 2f;
            float t = 0.5f + (offset * initialSpacing * scaleFactor) / splineLength;
            t = Mathf.Clamp01(t);
            Vector3 position = splineContainer.Spline.EvaluatePosition(t);
            cardTransform.localPosition = position;
            cardTransform.LookAt(lookAtObject.position + new Vector3(0, -2, 0));
            i++;
        }
    }
    
    private void HandleBurnMerged()
    {
        foreach (var (cardId, card) in CardsDictionary)
        {
            if (card.TokenType == CardData.TokenType.ManyDigits)
            {
                CoroutineHelper.Start(BurnCard(cardId));
            }
        }
    }
    protected override void HandleCardData(EnemyKnowledgeData data)
    {
        switch (SelfContainerKey.OwnerType)
        {
            case OwnerType.Enemy:
                data.selfHandCardsDictionary = new Dictionary<int, Card>(CardsDictionary);
                break;
            case OwnerType.Player:
                data.playerHandCardsCount = currentCardCount;
                break;
        }
    }
}
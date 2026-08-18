using System.Numerics;
using Content.Client.Message;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Utopia.Audio.UI;

public sealed class NowPlayingPopup : PanelContainer
{
    private enum PopupState
    {
        SlideIn,
        Hold,
        SlideOut,
        Done
    }

    private const float SlideInDuration = 0.5f;
    private const float HoldDuration = 4.0f;
    private const float SlideOutDuration = 0.5f;

    private const float PopupWidth = 380f;
    private const float PopupHeight = 72f;
    private const float HiddenOffset = PopupWidth + 16f;
    private const float ContentMargin = 8f;

    private PopupState _state = PopupState.SlideIn;
    private float _stateTime;
    private bool _orphanScheduled;

    public NowPlayingPopup(string markup)
    {
        MouseFilter = MouseFilterMode.Ignore;
        SetSize = new Vector2(PopupWidth, PopupHeight);

        var styleBox = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#25252AE6")
        };

        styleBox.SetContentMarginOverride(StyleBox.Margin.Top, ContentMargin);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Bottom, ContentMargin);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Left, ContentMargin);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Right, ContentMargin);

        PanelOverride = styleBox;

        var label = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        label.SetMarkup(markup);

        AddChild(label);
        SetPositionForFrame(-HiddenOffset);
    }

    public void CenterVertically(float parentHeight)
    {
        var top = parentHeight / 8f;
        LayoutContainer.SetMarginTop(this, top);
        LayoutContainer.SetMarginBottom(this, top + PopupHeight);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Parent == null || _state == PopupState.Done)
            return;

        _stateTime += args.DeltaSeconds;

        var xPosition = CalculateXPosition();
        SetPositionForFrame(xPosition);
    }

    private float CalculateXPosition()
    {
        switch (_state)
        {
            case PopupState.SlideIn:
            {
                var progress = Math.Clamp(_stateTime / SlideInDuration, 0f, 1f);
                if (progress >= 1f)
                    TransitionTo(PopupState.Hold);

                return MathHelper.Lerp(-HiddenOffset, 0f, EaseOutCubic(progress));
            }

            case PopupState.Hold:
            {
                if (_stateTime >= HoldDuration)
                    TransitionTo(PopupState.SlideOut);

                return 0f;
            }

            case PopupState.SlideOut:
            {
                var progress = Math.Clamp(_stateTime / SlideOutDuration, 0f, 1f);
                if (progress >= 1f)
                {
                    TransitionTo(PopupState.Done);
                    DeferOrphan();
                }

                return MathHelper.Lerp(0f, -HiddenOffset, EaseInCubic(progress));
            }

            default:
                return -HiddenOffset;
        }
    }

    private void TransitionTo(PopupState nextState)
    {
        _state = nextState;
        _stateTime = 0f;
    }

    private void DeferOrphan()
    {
        if (_orphanScheduled)
            return;

        _orphanScheduled = true;
        Timer.Spawn(0, Orphan);
    }

    private void SetPositionForFrame(float x)
    {
        LayoutContainer.SetMarginLeft(this, x);
        LayoutContainer.SetMarginRight(this, x + PopupWidth);
    }

    private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3);
    private static float EaseInCubic(float t) => t * t * t;
}
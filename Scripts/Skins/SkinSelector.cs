using Godot;

namespace Pluma.Scripts;

public partial class SkinSelector : Control
{
    private Button _leftButton;
    private Button _rightButton;
    private Label _skinNameLabel;

    public override void _Ready()
    {
        _leftButton = GetNode<Button>("VBoxContainer/HBoxContainer/LeftButton");
        _rightButton = GetNode<Button>("VBoxContainer/HBoxContainer/RightButton");
        _skinNameLabel = GetNode<Label>("VBoxContainer/HBoxContainer2/SkinNameLabel");


        _leftButton.MouseFilter = Control.MouseFilterEnum.Stop;
        _rightButton.MouseFilter = Control.MouseFilterEnum.Stop;
        _skinNameLabel.MouseFilter = Control.MouseFilterEnum.Stop;


        _leftButton.Pressed += () => { PlumaSkins.SetIndex(0); UpdateSelectedSkin(); };
        _rightButton.Pressed += () => { PlumaSkins.SetIndex(1); UpdateSelectedSkin(); };

        UpdateSelectedSkin();
    }

    private void UpdateSelectedSkin()
    {
        var skin = PlumaSkins.Current;
        _skinNameLabel.Text = skin.Name;

        _leftButton.Modulate = PlumaSkins.CurrentIndex == 0 ? Colors.White : new Color(0.5f, 0.5f, 0.5f);
        _rightButton.Modulate = PlumaSkins.CurrentIndex == 1 ? Colors.White : new Color(0.5f, 0.5f, 0.5f);
    }
}
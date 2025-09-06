using Godot;

[RequireNode("res://player.tscn", false)]
[GlobalClass]
public partial class MovementController : Node
{
    [Export] private float _speed = 50f;
    private CharacterBody2D _cb;

    public override void _EnterTree()
    {
        _cb = GetParent<CharacterBody2D>();
    }

    public override void _PhysicsProcess(double delta)
    {
        float x = Input.GetAxis("ui_left", "ui_right");
        Vector2 velocity = _cb.Velocity;
        velocity.X = x * _speed;
        _cb.Velocity = velocity;
        _cb.MoveAndSlide();
    }

}

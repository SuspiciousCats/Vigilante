using Godot;
using System;

/**
 * Character not controlled by player
 */
public class AICharacter : Character
{
	[Export(PropertyHint.None, "How long will ai wait before turning around")]
	/**
	 * How long will ai wait before turning around
	 * */
	public float TurnAroundWaitTime = 1f;

	[Export]
	public bool WalkRight = true;

	protected bool canWalk = true;

	protected Area2D groundDetection;

	protected Timer turnAroundWaitTimer;

	public override void _Ready()
	{
		base._Ready();
		groundDetection = GetNodeOrNull<Area2D>("FloorDetection") ?? throw new NullReferenceException("AI characters need to have ground detection Area2D to be able to walk");

		turnAroundWaitTimer = new Timer();
		turnAroundWaitTimer.Connect("timeout", this, nameof(TurnAround));
		AddChild(turnAroundWaitTimer);
	}

	protected override void SetCharacterMovementScale(Vector2 scale)
	{
		base.SetCharacterMovementScale(scale);
		if (Mathf.Abs(scale.x) >= 1 && Mathf.Abs(scale.y) >= 1)
		{
			groundDetection.Scale = scale;
		}
	}

	public void TurnAround()
	{
		WalkRight = !WalkRight;
		canWalk = true;
		turnAroundWaitTimer.Stop();
	}

	public override void _PhysicsProcess(float delta)
	{
		base._PhysicsProcess(delta);
		//we only need to set the speed with which the charcater is walking
		velocity.x = canWalk ? MovementSpeed * (WalkRight ? 1 : -1) : 0;
	}

	private void _on_FloorDetection_body_entered(Node2D body)
	{
		//ground is the only ground considered good for walking
		if (!body.IsInGroup("Ground"))
		{
			canWalk = false;
			turnAroundWaitTimer.Start(TurnAroundWaitTime);
		}
		GD.Print(body.Name);
	}
	private void _on_FloorDetection_body_exited(Node2D body)
	{
		//if detection doesn't detect ground any more -> stop, don't touch it, call an adult
		if (body.IsInGroup("Ground") && groundDetection.GetOverlappingBodies().Count == 0)
		{
			canWalk = false;
			turnAroundWaitTimer.Start(TurnAroundWaitTime);
			GD.Print("stop, don't touch it, call an adult");
		}
	}

}




using Godot;
using System;

/**
 * Character controlled by player
 */
public class PlayerCharacter : Character
{
	protected Timer attackResetTimer;

	private bool _firstAttack = true;

	[Export]
	public bool ControlledByPlayed = true;

	[Export]
	public float AttackResetTime = 0.5f;

	protected void ResetAttack()
	{
		//GD.Print("ResetAttack");
		currentAttackCount = 0;
		attackResetTimer.Stop();
	}


	public override void _Ready()
	{
		base._Ready();
		if (attackResetTimer == null)
		{
			attackResetTimer = new Timer();
			attackResetTimer.WaitTime = AttackResetTime;
			attackResetTimer.Connect("timeout", this, "ResetAttack");
			AddChild(attackResetTimer);
		}

		GetNode<Camera2D>("Camera2D").Current = ControlledByPlayed;
	}

	protected override bool isRunning()
	{
		return Input.IsActionPressed("run");

	}

	protected override void Attack()
	{
		if (!isBeingDamaged && !Dead)
		{
			base.Attack();

			animatedSprite.Play(GetAttackAnimation());

			isAttacking = true;
			_firstAttack = false;

			var enemies = attackArea.GetOverlappingBodies();
			foreach (Node2D enemy in enemies)
			{
				if (enemy.IsInGroup("Character"))
				{
					enemy.Call("BeDamaged", this, currentAttackCount, 1/*damage*/);
				}
			}
			currentAttackCount++;
			attackResetTimer.Paused = true;
		}
	}
	protected virtual void UpdateInput(float delta)
	{

		if (CanUpdateAnimation())
		{
			//only update velocity if we are on the ground where we can control it
			//we also don't reset it, this way player's landing postion will be based on how fast were they running
			if (lastIsOnTheGround)
			{
				//reset velocity, because movement is meant to be snappy
				velocity.x = 0;

				if (Input.IsActionPressed("move_right"))
				{
					//is player is holding run we start to run
					//Note: player can not run in the air so we prevent that
					velocity.x += MovementSpeed * (Input.IsActionPressed("run") ? 2 : 1);
				}

				if (Input.IsActionPressed("move_left"))
				{
					velocity.x -= MovementSpeed * (Input.IsActionPressed("run") ? 2 : 1);
				}

				if (Input.IsActionJustPressed("jump"))
				{
					velocity.y += JumpForce;
					PlayAnimation("Jump_Start");
				}
			}
		}

		if (Input.IsActionJustPressed("attack") && !isAttacking)
		{
			Attack();
		}
	}

	protected override void onAnimatedSpriteAnimationFinished(string animName)
	{
		base.onAnimatedSpriteAnimationFinished(animName);
		attackResetTimer.Paused = false;
		if (attackResetTimer.IsStopped() || _firstAttack)
		{
			attackResetTimer.Start();
		}
	}

	public override void _PhysicsProcess(float delta)
	{
		if (ControlledByPlayed)
		{
			UpdateInput(delta);
		}
		base._PhysicsProcess(delta);
	}
}


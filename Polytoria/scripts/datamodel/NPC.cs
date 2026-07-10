// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Godot.Collections;
using Polytoria.Attributes;
using Polytoria.Client;
using Polytoria.Networking;
using Polytoria.Scripting;
using Polytoria.Shared;
using Polytoria.Utils;

namespace Polytoria.Datamodel;

[Instantiable]
public partial class NPC : Instance
{
	private const float NavigationDistance = 2f;
	public const float BodyRotateLerp = 10f;
	private CharacterModel? _character;
	private Dynamic? _moveTarget;

	private string _displayName = "";
	private Node3D? _navAgentContainer;
	private NavigationAgent3D? _navAgent;

	// Pending properties to apply to character
	private Color? _pendingHeadColor;
	private Color? _pendingTorsoColor;
	private Color? _pendingLeftArmColor;
	private Color? _pendingRightArmColor;
	private Color? _pendingLeftLegColor;
	private Color? _pendingRightLegColor;
	private int? _pendingFaceID;

	[Editable, ScriptProperty, NoSync, Attributes.Obsolete("Apply them to Character"), CloneIgnore]
	public Color HeadColor
	{
		get => (Character is PolytorianModel polytorian) ? polytorian.HeadColor : _pendingHeadColor ?? new Color();
		set
		{
			if (Character is PolytorianModel polytorian)
			{
				polytorian.HeadColor = value;
				_pendingHeadColor = null;
			}
			else
			{
				_pendingHeadColor = value;
			}
		}
	}

	[Editable, ScriptProperty, NoSync, Attributes.Obsolete("Apply them to Character instead"), CloneIgnore]
	public Color TorsoColor
	{
		get => (Character is PolytorianModel polytorian) ? polytorian.TorsoColor : _pendingTorsoColor ?? new Color();
		set
		{
			if (Character is PolytorianModel polytorian)
			{
				polytorian.TorsoColor = value;
				_pendingTorsoColor = null;
			}
			else
			{
				_pendingTorsoColor = value;
			}
		}
	}

	[Editable, ScriptProperty, NoSync, Attributes.Obsolete("Apply them to Character instead"), CloneIgnore]
	public Color LeftArmColor
	{
		get => (Character is PolytorianModel polytorian) ? polytorian.LeftArmColor : _pendingLeftArmColor ?? new Color();
		set
		{
			if (Character is PolytorianModel polytorian)
			{
				polytorian.LeftArmColor = value;
				_pendingLeftArmColor = null;
			}
			else
			{
				_pendingLeftArmColor = value;
			}
		}
	}

	[Editable, ScriptProperty, NoSync, Attributes.Obsolete("Apply them to Character instead"), CloneIgnore]
	public Color RightArmColor
	{
		get => (Character is PolytorianModel polytorian) ? polytorian.RightArmColor : _pendingRightArmColor ?? new Color();
		set
		{
			if (Character is PolytorianModel polytorian)
			{
				polytorian.RightArmColor = value;
				_pendingRightArmColor = null;
			}
			else
			{
				_pendingRightArmColor = value;
			}
		}
	}

	[Editable, ScriptProperty, NoSync, Attributes.Obsolete("Apply them to Character instead"), CloneIgnore]
	public Color LeftLegColor
	{
		get => (Character is PolytorianModel polytorian) ? polytorian.LeftLegColor : _pendingLeftLegColor ?? new Color();
		set
		{
			if (Character is PolytorianModel polytorian)
			{
				polytorian.LeftLegColor = value;
				_pendingLeftLegColor = null;
			}
			else
			{
				_pendingLeftLegColor = value;
			}
		}
	}

	[Editable, ScriptProperty, NoSync, Attributes.Obsolete("Apply them to Character instead"), CloneIgnore]
	public Color RightLegColor
	{
		get => (Character is PolytorianModel polytorian) ? polytorian.RightLegColor : _pendingRightLegColor ?? new Color();
		set
		{
			if (Character is PolytorianModel polytorian)
			{
				polytorian.RightLegColor = value;
				_pendingRightLegColor = null;
			}
			else
			{
				_pendingRightLegColor = value;
			}
		}
	}

	[Editable, ScriptProperty, NoSync, Attributes.Obsolete("Apply them to Character instead"), CloneIgnore]
	public int FaceID
	{
		get => (Character is PolytorianModel polytorian) ? polytorian.FaceID : _pendingFaceID ?? 0;
		set
		{
			if (Character is PolytorianModel polytorian)
			{
				polytorian.FaceID = value;
				_pendingFaceID = null;
			}
			else
			{
				_pendingFaceID = value;
			}
		}
	}

	[Editable, ScriptProperty]
	public string DisplayName
	{
		get => _displayName;
		set
		{
			_displayName = value;
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, SyncVar]
	public CharacterModel? Character
	{
		get
		{
			if (_character != null && _character.IsDeleted)
			{
				_character = null;
			}
			return _character;
		}
		set
		{
			_character?.Controller = null;
			_character = value;
			value?.Controller = this;
			OnPropertyChanged();
		}
	}

	[SyncVar, ScriptProperty]
	public Dynamic? MoveTarget
	{
		get
		{
			if (_moveTarget != null && _moveTarget.IsDeleted)
			{
				_moveTarget = null;
			}
			return _moveTarget;
		}
		set => _moveTarget = value;
	}

	[ScriptProperty] public float NavDestinationDistance => _navAgent == null ? Mathf.Inf : _navAgent.DistanceToTarget();

	[ScriptProperty]
	public bool NavDestinationReached { get; private set; } = false;

	[ScriptProperty] public bool NavDestinationValid => _navAgent != null && _navAgent.IsTargetReachable();

	[ScriptProperty]
	public PTSignal NavFinished { get; private set; } = new();

	public override void PreDelete()
	{
		_navAgent?.NavigationFinished -= OnNavFinished;
		base.PreDelete();
	}

	public void Navigate(double delta)
	{
		if (Character == null) return;

		bool isOnFloor = Character.IsOnGround;
		bool isOnCeiling = Character.IsOnCeiling;
		bool playerNPCOverride = Character != null && !Character.CanMove;

		CharacterModel.CharacterModelStateEnum finalState = CharacterModel.CharacterModelStateEnum.Idle;
		Vector3? walkTarget = null;
		float animSpeed = 1;

		if (MoveTarget != null)
		{
			walkTarget = MoveTarget.GetGlobalPosition();
		}

		if (_navAgent != null)
		{
			walkTarget = _navAgent.GetNextPathPosition();

			// Adjust Nav agent position in-case of unstable Y position changes
			_navAgentContainer?.GlobalPosition = _navAgentContainer.GlobalPosition with { Y = walkTarget.Value.Y };
		}

		if (walkTarget.HasValue)
		{
			Vector3 velo = Character.GetGlobalPosition().DirectionTo(walkTarget.Value with { Y = Character.Position.Y });
			Character.CharacterVelocity = new(velo.X * Character.WalkSpeed, Character.CharacterVelocity.Y, velo.Z * Character.WalkSpeed);
			Character.GDNode3D.GlobalRotationDegrees = new Vector3(Character.Rotation.X, Mathf.RadToDeg(Mathf.LerpAngle(Mathf.DegToRad(Character.Rotation.Y), Mathf.Atan2(Character.CharacterVelocity.X, Character.CharacterVelocity.Z), MathUtils.ExpDecay((float)delta, BodyRotateLerp))), Character.Rotation.Z);

			float distanceToTarget = Character.GetGlobalPosition().DistanceTo(walkTarget.Value);

			if (distanceToTarget > 0.5f)
			{
				finalState = CharacterModel.CharacterModelStateEnum.Walking;
				animSpeed = Character.WalkSpeed / 8;
				Character.TryStepUp();
			}
		}
		else if (this is not Player || playerNPCOverride)
		{
			Character.CharacterVelocity = new(0, Character.CharacterVelocity.Y, 0);
		}

		if (!isOnFloor)
		{
			finalState = CharacterModel.CharacterModelStateEnum.Jumping;
		}

		if (this is not Player || playerNPCOverride)
		{
			Character.SetState(finalState);
			Character.SetAnimSpeed(animSpeed);
		}
	}

	public override void Ready()
	{
		if (Root.IsLegacyWorld && Character == null && !PendingProps.Contains(nameof(Character)))
		{
			// Create default character on legacy world. If character is not set
			Root.Insert.InitializeDefaultNPC(this);

			if (Character is PolytorianModel polytorian)
			{
				if (_pendingHeadColor.HasValue)
				{
					polytorian.HeadColor = _pendingHeadColor.Value;
					_pendingHeadColor = null;
				}
				if (_pendingTorsoColor.HasValue)
				{
					polytorian.TorsoColor = _pendingTorsoColor.Value;
					_pendingTorsoColor = null;
				}
				if (_pendingLeftArmColor.HasValue)
				{
					polytorian.LeftArmColor = _pendingLeftArmColor.Value;
					_pendingLeftArmColor = null;
				}
				if (_pendingRightArmColor.HasValue)
				{
					polytorian.RightArmColor = _pendingRightArmColor.Value;
					_pendingRightArmColor = null;
				}
				if (_pendingLeftLegColor.HasValue)
				{
					polytorian.LeftLegColor = _pendingLeftLegColor.Value;
					_pendingLeftLegColor = null;
				}
				if (_pendingRightLegColor.HasValue)
				{
					polytorian.RightLegColor = _pendingRightLegColor.Value;
					_pendingRightLegColor = null;
				}
				if (_pendingFaceID.HasValue)
				{
					polytorian.FaceID = _pendingFaceID.Value;
					_pendingFaceID = null;
				}
			}
		}

		if (Character != null) Character.Ready();
		base.Ready();
	}

#if CREATOR
	public override void CreatorInserted()
	{
		Root.Insert.InitializeDefaultNPC(this);
		base.CreatorInserted();
	}
#endif

	[ScriptMethod]
	public void SetNavDestination(Vector3 pos)
	{
		MoveTarget = null;
		if (Character == null) return;
		if (_navAgent == null)
		{
			_navAgentContainer = new();
			_navAgent = new()
			{
				PathDesiredDistance = NavigationDistance,
				TargetDesiredDistance = 0.5f,
				PathHeightOffset = -(Character.CalculateBounds().Size.Y / 2),
				PathMaxDistance = 3f
			};

			_navAgentContainer.AddChild(_navAgent);
			Character.GDNode3D.AddChild(_navAgentContainer);
			if (Globals.IsInGDEditor)
			{
				_navAgent.DebugEnabled = true;
			}

			_navAgent.NavigationFinished += OnNavFinished;
			NavDestinationReached = false;
		}
		_navAgent.TargetPosition = pos;
	}

	private void OnNavFinished()
	{
		_navAgentContainer?.QueueFree();
		_navAgent = null;
		NavDestinationReached = true;
		NavFinished.Invoke();
	}
}

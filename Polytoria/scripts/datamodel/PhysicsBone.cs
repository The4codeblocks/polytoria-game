// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Attributes;
using Polytoria.Utils;
using System;

namespace Polytoria.Datamodel;

[Instantiable]
public partial class PhysicsBone : Physical
{
	internal PhysicalBone3D GDPhysicalBone = null!;

	private float _gravityScale;
	private float _mass;
	private float _friction;
	private float _drag;
	private float _angularDrag;
	private float _bounciness;
	private float _lastdt;

	[Editable, ScriptProperty, SyncVar(Unreliable = true, AllowAuthorWrite = true)]
	public override Vector3 Velocity
	{
		get
		{
			return GDPhysicalBone.LinearVelocity;
		}
		set
		{
			GDPhysicalBone.LinearVelocity = value;
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, SyncVar(Unreliable = true, AllowAuthorWrite = true)]
	public override Vector3 AngularVelocity
	{
		get
		{
			return GDPhysicalBone.AngularVelocity.FlipEuler();
		}
		set
		{
			GDPhysicalBone.AngularVelocity = value.FlipEuler();
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, DefaultValue(1f)]
	public float GravityScale
	{
		get => _gravityScale;
		set
		{
			if (_gravityScale == value)
			{
				return;
			}

			_gravityScale = value;

			GDPhysicalBone.GravityScale = value * 2f;

			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, DefaultValue(1f)]
	public float Mass
	{
		get => _mass;
		set
		{
			if (_mass == value)
			{
				return;
			}

			_mass = value;

			GDPhysicalBone.Mass = Math.Max(_mass, Physical.MinMass);

			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, DefaultValue(0)]
	public float Drag
	{
		get => _drag;
		set
		{
			if (_drag == value)
			{
				return;
			}

			_drag = value;
			GDPhysicalBone.LinearDamp = value;
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, DefaultValue(0)]
	public float AngularDrag
	{
		get => _angularDrag;
		set
		{
			if (_angularDrag == value)
			{
				return;
			}

			_angularDrag = value;
			GDPhysicalBone.AngularDamp = value;
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, DefaultValue(0)]
	public float Bounciness
	{
		get => _bounciness;
		set
		{
			if (_bounciness == value)
			{
				return;
			}

			_bounciness = value;

			GDPhysicalBone.Bounce = value;

			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, DefaultValue(0.6f)]
	public float Friction
	{
		get => _friction;
		set
		{
			if (_friction == value)
			{
				return;
			}

			_friction = value;
			GDPhysicalBone.Friction = value;
			OnPropertyChanged();
		}
	}

	public override Node CreateGDNode()
	{
		return new PhysicalBone3D();
	}

	public override void InitGDNode()
	{
		base.InitGDNode();
		GDPhysicalBone = (PhysicalBone3D)GDNode;
		GDPhysicalBone.GravityScale = 2;
	}

	public override void Init()
	{
		SetPhysicsProcess(true);
		base.Init();
		CanCollide = true;
	}

	public override void PhysicsProcess(double delta)
	{
		base.PhysicsProcess(delta);
		_lastdt = (float)delta;
	}

	internal override void ApplyAddForce(Vector3 force, ForceModeEnum mode = ForceModeEnum.Force)
	{
		if (mode == ForceModeEnum.Force)
		{
			GDPhysicalBone.ApplyCentralImpulse(force * _lastdt);
		}
		else if (mode == ForceModeEnum.Acceleration)
		{
			GDPhysicalBone.ApplyCentralImpulse(force * (_lastdt * _mass));
		}
		else if (mode == ForceModeEnum.Impulse)
		{
			GDPhysicalBone.ApplyCentralImpulse(force);
		}
		else if (mode == ForceModeEnum.VelocityChange)
		{
			GDPhysicalBone.ApplyCentralImpulse(force * _mass);
		}
		else
		{
			throw new NotImplementedException(mode + " not implemented");
		}
	}

	internal override void ApplyAddForceAtPosition(Vector3 force, Vector3 position, ForceModeEnum mode = ForceModeEnum.Force)
	{
		if (mode == ForceModeEnum.Force)
		{
			GDPhysicalBone.ApplyImpulse(force * _lastdt, position);
		}
		else if (mode == ForceModeEnum.Acceleration)
		{
			GDPhysicalBone.ApplyImpulse(force * (_lastdt * _mass), position);
		}
		else if (mode == ForceModeEnum.Impulse)
		{
			GDPhysicalBone.ApplyImpulse(force, position);
		}
		else if (mode == ForceModeEnum.VelocityChange)
		{
			GDPhysicalBone.ApplyImpulse(force * _mass, position);
		}
		else
		{
			throw new NotImplementedException(mode + " not implemented");
		}
	}

	internal override void ApplyAddRelativeForce(Vector3 force, ForceModeEnum mode = ForceModeEnum.Force)
	{
		Vector3 worldForce = GDPhysicalBone.GlobalTransform.Basis * force;
		if (mode == ForceModeEnum.Force)
		{
			GDPhysicalBone.ApplyCentralImpulse(worldForce * _lastdt);
		}
		else if (mode == ForceModeEnum.Acceleration)
		{
			GDPhysicalBone.ApplyCentralImpulse(worldForce * (_lastdt * _mass));
		}
		else if (mode == ForceModeEnum.Impulse)
		{
			GDPhysicalBone.ApplyCentralImpulse(worldForce);
		}
		else if (mode == ForceModeEnum.VelocityChange)
		{
			GDPhysicalBone.ApplyCentralImpulse(worldForce * _mass);
		}
		else
		{
			throw new NotImplementedException(mode + " not implemented");
		}
	}
}

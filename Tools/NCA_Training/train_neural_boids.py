"""
train_neural_boids.py — Train learned boid update rules from trajectory data.

This applies the Growing NCA methodology to continuous 3D agents:
- Local perception: each boid aggregates neighbor statistics
- Learned update rule: small NN computes velocity deltas
- Trained against target trajectory data (e.g., starling murmuration recordings)

Usage:
    python train_neural_boids.py --target trajectory_data.npz --output boid_weights.bytes --steps 5000
    python train_neural_boids.py --synthetic --output boid_weights.bytes --steps 5000

Target data format (.npz):
    positions: [T, N, 3] — positions over T timesteps for N agents
    velocities: [T, N, 3] — velocities over T timesteps for N agents

The --synthetic flag generates a simple murmuration-like target for testing.
"""

import argparse
import os
import numpy as np

try:
    import torch
    import torch.nn as nn
    import torch.nn.functional as F
    HAS_TORCH = True
except ImportError:
    HAS_TORCH = False


def generate_synthetic_murmuration(n_agents=100, n_steps=200, dt=0.016):
    """
    Generate synthetic murmuration-like trajectories for training.
    Uses Reynolds' rules with additional terms for the characteristic
    shifting, swirling behavior of starling murmurations.
    """
    positions = np.zeros((n_steps, n_agents, 3), dtype=np.float32)
    velocities = np.zeros((n_steps, n_agents, 3), dtype=np.float32)

    # Initialize in a sphere
    positions[0] = np.random.randn(n_agents, 3).astype(np.float32) * 20
    velocities[0] = np.random.randn(n_agents, 3).astype(np.float32) * 2

    # Murmuration attractor — slowly orbiting goal
    for t in range(1, n_steps):
        # Goal orbits in a circle
        angle = t * 0.02
        goal = np.array([np.cos(angle) * 50, np.sin(angle * 0.7) * 30, np.sin(angle) * 50])

        for i in range(n_agents):
            pos = positions[t-1, i]
            vel = velocities[t-1, i]

            sep = np.zeros(3)
            coh = np.zeros(3)
            ali = np.zeros(3)
            count = 0

            for j in range(n_agents):
                if i == j:
                    continue
                diff = positions[t-1, j] - pos
                dist = np.linalg.norm(diff)
                if dist < 30 and dist > 0.1:
                    coh += diff
                    ali += velocities[t-1, j]
                    count += 1
                    if dist < 5:
                        sep -= diff / (dist * dist)

            if count > 0:
                coh = (coh / count - pos) * 0.01
                ali = (ali / count - vel) * 0.05

            goal_dir = (goal - pos) * 0.002

            # Swirl term — characteristic of murmurations
            swirl = np.cross(vel, np.array([0, 1, 0])) * 0.01

            accel = sep * 0.8 + coh + ali + goal_dir + swirl
            new_vel = vel + accel * dt

            speed = np.linalg.norm(new_vel)
            if speed > 8:
                new_vel = new_vel / speed * 8
            elif speed < 1:
                new_vel = new_vel / (speed + 1e-6) * 1

            velocities[t, i] = new_vel
            positions[t, i] = pos + new_vel * dt

    return positions, velocities


class NeuralBoidUpdateRule(nn.Module):
    """
    perception(13) -> dense(hidden, ReLU) -> dense(3, linear) -> velocity_delta

    Mirrors the NCA update rule architecture but for continuous 3D agents.
    """

    def __init__(self, input_dim=13, hidden_size=64, output_dim=3):
        super().__init__()
        self.fc1 = nn.Linear(input_dim, hidden_size)
        self.fc2 = nn.Linear(hidden_size, output_dim)
        nn.init.zeros_(self.fc2.weight)
        nn.init.zeros_(self.fc2.bias)

    def forward(self, x):
        x = F.relu(self.fc1(x))
        return self.fc2(x)


def compute_perception(positions, velocities, idx, perception_radius=30.0, max_neighbors=16):
    """
    Compute the aggregated perception vector for a single boid.
    Analogous to NCA's Sobel perception but for continuous 3D space.
    """
    n = positions.shape[0]
    pos = positions[idx]
    vel = velocities[idx]

    avg_rel_pos = np.zeros(3)
    avg_rel_vel = np.zeros(3)
    closest_rel_pos = np.zeros(3)
    closest_dist = perception_radius * 2
    avg_dist = 0
    density = 0
    count = 0
    max_speed = 8.0

    for j in range(n):
        if j == idx:
            continue
        diff = positions[j] - pos
        dist = np.linalg.norm(diff)
        if dist < perception_radius and dist > 0.001 and count < max_neighbors:
            avg_rel_pos += diff
            avg_rel_vel += velocities[j] - vel
            avg_dist += dist
            if dist < closest_dist:
                closest_dist = dist
                closest_rel_pos = diff
            density += 1.0 / (dist + 0.1)
            count += 1

    if count > 0:
        avg_rel_pos /= count
        avg_rel_vel /= count
        avg_dist /= count

    speed = np.linalg.norm(vel)

    return np.array([
        avg_rel_pos[0], avg_rel_pos[1], avg_rel_pos[2],
        avg_rel_vel[0], avg_rel_vel[1], avg_rel_vel[2],
        closest_rel_pos[0], closest_rel_pos[1], closest_rel_pos[2],
        avg_dist / perception_radius,
        density,
        speed / max_speed,
        count / max_neighbors,
    ], dtype=np.float32)


def export_pytorch_weights(model, output_path):
    """Export to Unity .bytes format."""
    state = model.state_dict()
    w1 = state['fc1.weight'].cpu().numpy().T
    b1 = state['fc1.bias'].cpu().numpy()
    w2 = state['fc2.weight'].cpu().numpy().T
    b2 = state['fc2.bias'].cpu().numpy()

    flat = np.concatenate([
        w1.flatten().astype(np.float32),
        b1.flatten().astype(np.float32),
        w2.flatten().astype(np.float32),
        b2.flatten().astype(np.float32),
    ])

    os.makedirs(os.path.dirname(output_path) or '.', exist_ok=True)
    flat.tofile(output_path)
    print(f"Exported {len(flat)} floats ({len(flat) * 4} bytes) to {output_path}")


def train(args):
    if not HAS_TORCH:
        print("PyTorch required. Install: pip install torch")
        return

    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    print(f"Training on {device}")

    # Load or generate trajectory data
    if args.synthetic:
        print("Generating synthetic murmuration data...")
        positions, velocities = generate_synthetic_murmuration(
            n_agents=args.agents, n_steps=args.traj_steps)
    else:
        data = np.load(args.target)
        positions = data['positions']
        velocities = data['velocities']

    n_steps, n_agents, _ = positions.shape
    print(f"Trajectory: {n_steps} steps, {n_agents} agents")

    # Precompute perception vectors and target velocity deltas
    print("Precomputing perception vectors...")
    perception_data = []
    delta_data = []

    for t in range(0, n_steps - 1, max(1, n_steps // 100)):
        for i in range(n_agents):
            p = compute_perception(positions[t], velocities[t], i)
            target_delta = velocities[t + 1, i] - velocities[t, i]
            perception_data.append(p)
            delta_data.append(target_delta)

    X = torch.from_numpy(np.stack(perception_data)).to(device)
    Y = torch.from_numpy(np.stack(delta_data)).to(device)
    print(f"Training samples: {X.shape[0]}")

    # Model
    model = NeuralBoidUpdateRule(input_dim=13, hidden_size=args.hidden, output_dim=3).to(device)
    optimizer = torch.optim.Adam(model.parameters(), lr=1e-3)
    scheduler = torch.optim.lr_scheduler.StepLR(optimizer, step_size=1000, gamma=0.5)

    # Train
    batch_size = 256
    for step in range(args.steps):
        idx = np.random.choice(len(X), min(batch_size, len(X)), replace=False)
        x_batch = X[idx]
        y_batch = Y[idx]

        pred = model(x_batch)
        loss = F.mse_loss(pred, y_batch)

        optimizer.zero_grad()
        loss.backward()
        # Gradient normalization (from NCA paper)
        with torch.no_grad():
            total_norm = 0.0
            for p in model.parameters():
                if p.grad is not None:
                    total_norm += p.grad.norm().item() ** 2
            total_norm = total_norm ** 0.5
            if total_norm > 0:
                for p in model.parameters():
                    if p.grad is not None:
                        p.grad /= (total_norm + 1e-8)
        optimizer.step()
        scheduler.step()

        if step % 200 == 0:
            print(f"Step {step}/{args.steps}, Loss: {loss.item():.6f}")

    export_pytorch_weights(model, args.output)
    print("Training complete.")


def main():
    parser = argparse.ArgumentParser(description='Train Neural Boid update rule')
    parser.add_argument('--target', type=str, default=None,
                       help='Path to trajectory .npz file')
    parser.add_argument('--synthetic', action='store_true',
                       help='Generate synthetic murmuration data instead of loading')
    parser.add_argument('--output', type=str, required=True,
                       help='Output .bytes path for Unity')
    parser.add_argument('--steps', type=int, default=5000,
                       help='Training steps')
    parser.add_argument('--agents', type=int, default=100,
                       help='Number of agents for synthetic data')
    parser.add_argument('--traj_steps', type=int, default=200,
                       help='Number of trajectory timesteps for synthetic data')
    parser.add_argument('--hidden', type=int, default=64,
                       help='Hidden layer size')

    args = parser.parse_args()
    if not args.synthetic and not args.target:
        parser.error("Either --target or --synthetic is required")
    train(args)


if __name__ == '__main__':
    main()

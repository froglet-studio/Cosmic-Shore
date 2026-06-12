"""
train_nca.py — Train a Growing Neural Cellular Automata model.
Based on distill.pub/2020/growing-ca/

Produces a .bytes weight file that can be loaded directly into Unity's NCASimulator.

Usage:
    python train_nca.py --target target_image.png --output trained_weights.bytes --steps 8000
    python train_nca.py --target animated_frames/ --output trained_weights.bytes --steps 8000

For animated targets, provide a directory of PNG frames.
"""

import argparse
import os
import numpy as np

try:
    import tensorflow as tf
    HAS_TF = True
except ImportError:
    HAS_TF = False

try:
    import torch
    import torch.nn as nn
    import torch.nn.functional as F
    HAS_TORCH = True
except ImportError:
    HAS_TORCH = False


# ── PyTorch Implementation ──────────────────────────────

class SobelPerception(nn.Module):
    """Fixed perception filters: identity + Sobel X + Sobel Y per channel."""

    def __init__(self, channel_count=16):
        super().__init__()
        self.channel_count = channel_count

        # Build 3x3 kernels: identity, sobel_x, sobel_y
        identity = torch.tensor([[0, 0, 0], [0, 1, 0], [0, 0, 0]], dtype=torch.float32)
        sobel_x = torch.tensor([[-1, 0, 1], [-2, 0, 2], [-1, 0, 1]], dtype=torch.float32) / 8.0
        sobel_y = torch.tensor([[-1, -2, -1], [0, 0, 0], [1, 2, 1]], dtype=torch.float32) / 8.0

        # Stack into depthwise conv kernel: [out_channels, 1, 3, 3]
        # For each input channel, we produce 3 outputs (identity, sx, sy)
        kernels = torch.stack([identity, sobel_x, sobel_y])  # [3, 3, 3]
        kernels = kernels.unsqueeze(1)  # [3, 1, 3, 3]
        # Repeat for all channels: [3*C, 1, 3, 3]
        kernels = kernels.repeat(channel_count, 1, 1, 1)  # [3*C, 1, 3, 3]

        self.register_buffer('kernels', kernels)

    def forward(self, x):
        # x: [B, C, H, W]
        b, c, h, w = x.shape
        # Reshape to apply depthwise: [B*C, 1, H, W]
        x_flat = x.reshape(b * c, 1, h, w)
        # Apply all 3 kernels per channel
        # kernels: [3*C, 1, 3, 3] but we need [3, 1, 3, 3] applied per channel
        identity = torch.tensor([[0, 0, 0], [0, 1, 0], [0, 0, 0]], dtype=x.dtype, device=x.device).reshape(1, 1, 3, 3)
        sobel_x = torch.tensor([[-1, 0, 1], [-2, 0, 2], [-1, 0, 1]], dtype=x.dtype, device=x.device).reshape(1, 1, 3, 3) / 8.0
        sobel_y = torch.tensor([[-1, -2, -1], [0, 0, 0], [1, 2, 1]], dtype=x.dtype, device=x.device).reshape(1, 1, 3, 3) / 8.0

        # Apply each filter: [B*C, 1, H, W] -> [B*C, 1, H, W]
        id_out = F.conv2d(x_flat, identity, padding=1)
        sx_out = F.conv2d(x_flat, sobel_x, padding=1)
        sy_out = F.conv2d(x_flat, sobel_y, padding=1)

        # Stack and reshape: [B, 3*C, H, W]
        out = torch.cat([id_out, sx_out, sy_out], dim=1)  # [B*C, 3, H, W]
        out = out.reshape(b, 3 * c, h, w)
        return out


class NCAUpdateRule(nn.Module):
    """perception(48) -> dense(128, ReLU) -> dense(16, linear) -> dx"""

    def __init__(self, perception_dim=48, hidden_size=128, channel_count=16):
        super().__init__()
        self.fc1 = nn.Linear(perception_dim, hidden_size)
        self.fc2 = nn.Linear(hidden_size, channel_count)
        # Zero-initialize output layer (as per the paper)
        nn.init.zeros_(self.fc2.weight)
        nn.init.zeros_(self.fc2.bias)

    def forward(self, perception):
        # perception: [B, 48, H, W]
        b, c, h, w = perception.shape
        # Reshape to [B*H*W, 48] for dense layers
        x = perception.permute(0, 2, 3, 1).reshape(-1, c)
        x = F.relu(self.fc1(x))
        x = self.fc2(x)
        # Reshape back to [B, 16, H, W]
        return x.reshape(b, h, w, -1).permute(0, 3, 1, 2)


class GrowingNCA(nn.Module):
    def __init__(self, channel_count=16, hidden_size=128, fire_rate=0.5):
        super().__init__()
        self.channel_count = channel_count
        self.fire_rate = fire_rate
        self.perception = SobelPerception(channel_count)
        self.update_rule = NCAUpdateRule(channel_count * 3, hidden_size, channel_count)

    def alive_mask(self, x):
        """Cells are alive if any neighbor has alpha > 0.1"""
        alpha = x[:, 3:4, :, :]
        # Max-pool over 3x3 neighborhood
        alive = F.max_pool2d(alpha, 3, stride=1, padding=1) > 0.1
        return alive.float()

    def forward(self, x, steps=1):
        for _ in range(steps):
            pre_alive = self.alive_mask(x)

            # Perceive
            p = self.perception(x)

            # Update rule
            dx = self.update_rule(p)

            # Stochastic mask
            if self.training:
                mask = (torch.rand(x.shape[0], 1, x.shape[2], x.shape[3], device=x.device) <= self.fire_rate).float()
            else:
                mask = 1.0

            x = x + dx * mask

            # Alive masking
            post_alive = self.alive_mask(x)
            alive = pre_alive * post_alive
            x = x * alive

        return x


def load_target(path, size=40, padding=16):
    """Load target image(s) and convert to tensor."""
    from PIL import Image

    if os.path.isdir(path):
        frames = sorted([f for f in os.listdir(path) if f.endswith('.png')])
        images = []
        for f in frames:
            img = Image.open(os.path.join(path, f)).resize((size, size))
            images.append(np.array(img, dtype=np.float32) / 255.0)
        targets = np.stack(images)
    else:
        img = Image.open(path).resize((size, size))
        targets = np.array(img, dtype=np.float32)[np.newaxis] / 255.0

    if targets.ndim == 3:
        targets = targets[..., np.newaxis]
    if targets.shape[-1] == 3:
        alpha = np.ones((*targets.shape[:-1], 1), dtype=np.float32)
        targets = np.concatenate([targets, alpha], axis=-1)

    # Pad to full grid size
    total_size = size + 2 * padding
    padded = np.zeros((*targets.shape[:1], total_size, total_size, 4), dtype=np.float32)
    padded[:, padding:padding+size, padding:padding+size, :] = targets[:, :, :, :4]

    return torch.from_numpy(padded).permute(0, 3, 1, 2)  # [N, 4, H, W]


def make_seed(size, channel_count=16):
    """Create a single-pixel seed at the center."""
    seed = torch.zeros(1, channel_count, size, size)
    mid = size // 2
    seed[0, 3, mid, mid] = 1.0  # Alpha channel = 1 at center
    return seed


def export_pytorch_weights(model, output_path):
    """Export the trained update rule weights to Unity .bytes format."""
    state = model.update_rule.state_dict()

    w1 = state['fc1.weight'].cpu().numpy().T  # PyTorch: [out, in] -> [in, out]
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
        print("PyTorch required for training. Install: pip install torch torchvision pillow")
        return

    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    print(f"Training on {device}")

    target_size = 40
    padding = 16
    grid_size = target_size + 2 * padding
    channel_count = 16

    # Load target
    target = load_target(args.target, target_size, padding).to(device)
    num_frames = target.shape[0]
    print(f"Target: {num_frames} frame(s), grid size {grid_size}x{grid_size}")

    # Model
    model = GrowingNCA(channel_count=channel_count, hidden_size=128, fire_rate=0.5).to(device)
    optimizer = torch.optim.Adam(model.parameters(), lr=2e-3)

    # Sample pool
    pool_size = 1024
    seed = make_seed(grid_size, channel_count).to(device)
    pool = seed.repeat(pool_size, 1, 1, 1)

    for step in range(args.steps):
        # Adjust learning rate
        if step == 2000:
            for pg in optimizer.param_groups:
                pg['lr'] = 2e-4

        # Sample batch from pool
        batch_size = 8
        indices = np.random.choice(pool_size, batch_size, replace=False)
        batch = pool[indices].clone()

        # Replace one sample with seed
        batch[0] = seed[0]

        # Damage (for regeneration training)
        if args.damage and step > 500:
            for d in range(min(3, batch_size)):
                cx = np.random.randint(padding, grid_size - padding)
                cy = np.random.randint(padding, grid_size - padding)
                r = np.random.randint(5, 15)
                y, x = np.ogrid[:grid_size, :grid_size]
                mask = ((x - cx)**2 + (y - cy)**2) > r**2
                mask = torch.from_numpy(mask.astype(np.float32)).to(device)
                batch[d] *= mask.unsqueeze(0)

        # Forward: random number of steps
        n_steps = np.random.randint(64, 96)
        batch.requires_grad_(True)
        output = model(batch, steps=n_steps)

        # Loss on RGBA channels only
        frame_idx = step % num_frames
        target_batch = target[frame_idx:frame_idx+1].expand(batch_size, -1, -1, -1)
        loss = F.mse_loss(output[:, :4], target_batch)

        # Gradient normalization (from the paper)
        optimizer.zero_grad()
        loss.backward()
        with torch.no_grad():
            for p in model.parameters():
                if p.grad is not None:
                    grad_norm = p.grad.norm()
                    if grad_norm > 0:
                        p.grad /= (grad_norm + 1e-8)
        optimizer.step()

        # Update pool (sort by loss, replace worst)
        with torch.no_grad():
            per_sample_loss = F.mse_loss(output[:, :4], target_batch, reduction='none').mean(dim=(1, 2, 3))
            sorted_indices = per_sample_loss.argsort(descending=True)
            pool[indices] = output.detach()

        if step % 100 == 0:
            print(f"Step {step}/{args.steps}, Loss: {loss.item():.6f}")

    # Export
    export_pytorch_weights(model, args.output)
    print("Training complete.")


def main():
    parser = argparse.ArgumentParser(description='Train Growing NCA')
    parser.add_argument('--target', type=str, required=True,
                       help='Target image (PNG) or directory of frames for animated NCA')
    parser.add_argument('--output', type=str, required=True,
                       help='Output .bytes path for Unity')
    parser.add_argument('--steps', type=int, default=8000,
                       help='Training steps')
    parser.add_argument('--damage', action='store_true',
                       help='Enable damage training for regeneration')

    args = parser.parse_args()
    train(args)


if __name__ == '__main__':
    main()

"""
export_weights.py — Export trained NCA / Neural Boid weights for Unity consumption.

Usage (NCA):
    python export_weights.py nca --model path/to/model --output Assets/path/to/weights.bytes

Usage (Boid):
    python export_weights.py boid --model path/to/model --output Assets/path/to/weights.bytes

Usage (FlowField):
    python export_weights.py flow --model path/to/model --output Assets/path/to/weights.bytes

The output .bytes file is a flat array of float32 values laid out as:
    [weights_layer1, biases_layer1, weights_layer2, biases_layer2]

Weights are stored in row-major order matching the compute shader's indexing:
    weights[input_idx * output_size + output_idx]

This script can work with either TensorFlow or PyTorch models.
"""

import argparse
import struct
import sys
import os
import numpy as np


def export_two_layer_weights(w1, b1, w2, b2, output_path):
    """Export a two-layer network's weights to a flat .bytes file for Unity."""
    # Flatten and concatenate: [w1, b1, w2, b2]
    flat = np.concatenate([
        w1.flatten().astype(np.float32),
        b1.flatten().astype(np.float32),
        w2.flatten().astype(np.float32),
        b2.flatten().astype(np.float32),
    ])

    os.makedirs(os.path.dirname(output_path) or '.', exist_ok=True)
    flat.tofile(output_path)

    print(f"Exported {len(flat)} floats ({len(flat) * 4} bytes) to {output_path}")
    print(f"  Layer 1 weights: {w1.shape} ({w1.size} floats)")
    print(f"  Layer 1 biases:  {b1.shape} ({b1.size} floats)")
    print(f"  Layer 2 weights: {w2.shape} ({w2.size} floats)")
    print(f"  Layer 2 biases:  {b2.shape} ({b2.size} floats)")


def export_from_tensorflow(model_path, output_path, model_type):
    """Load a TensorFlow/Keras model and export weights."""
    import tensorflow as tf

    model = tf.keras.models.load_model(model_path)

    # The model should have exactly 2 Dense layers
    dense_layers = [l for l in model.layers if isinstance(l, tf.keras.layers.Dense)]
    if len(dense_layers) != 2:
        print(f"Error: Expected 2 Dense layers, found {len(dense_layers)}")
        sys.exit(1)

    w1, b1 = dense_layers[0].get_weights()
    w2, b2 = dense_layers[1].get_weights()

    export_two_layer_weights(w1, b1, w2, b2, output_path)


def export_from_pytorch(model_path, output_path, model_type):
    """Load a PyTorch model state dict and export weights."""
    import torch

    state = torch.load(model_path, map_location='cpu')

    # Look for common layer naming patterns
    w1_key = next(k for k in state.keys() if 'weight' in k and ('0' in k or 'fc1' in k or 'layer1' in k))
    b1_key = w1_key.replace('weight', 'bias')
    w2_key = next(k for k in state.keys() if 'weight' in k and ('2' in k or 'fc2' in k or 'layer2' in k))
    b2_key = w2_key.replace('weight', 'bias')

    # PyTorch stores weights as [out_features, in_features], we need [in, out]
    w1 = state[w1_key].numpy().T
    b1 = state[b1_key].numpy()
    w2 = state[w2_key].numpy().T
    b2 = state[b2_key].numpy()

    export_two_layer_weights(w1, b1, w2, b2, output_path)


def export_from_colab_checkpoint(model_path, output_path):
    """
    Export weights from the Growing CA Colab notebook's checkpoint format.
    The notebook saves the model as a TF checkpoint with variables like:
      ca/dense/kernel:0, ca/dense/bias:0, ca/dense_1/kernel:0, ca/dense_1/bias:0
    """
    import tensorflow as tf

    reader = tf.train.load_checkpoint(model_path)
    var_map = reader.get_variable_to_shape_map()

    # Find the two dense layer weights
    w1 = reader.get_tensor('ca/dense/kernel')      # (48, 128) for NCA
    b1 = reader.get_tensor('ca/dense/bias')         # (128,)
    w2 = reader.get_tensor('ca/dense_1/kernel')     # (128, 16)
    b2 = reader.get_tensor('ca/dense_1/bias')       # (16,)

    export_two_layer_weights(w1, b1, w2, b2, output_path)


def generate_random_weights(output_path, model_type):
    """Generate random weights for testing the Unity pipeline without training."""
    np.random.seed(42)

    if model_type == 'nca':
        # NCA: perception(48) -> hidden(128) -> channels(16)
        w1 = np.random.randn(48, 128).astype(np.float32) * 0.01
        b1 = np.zeros(128, dtype=np.float32)
        w2 = np.zeros((128, 16), dtype=np.float32)  # Zero-initialized like the paper
        b2 = np.zeros(16, dtype=np.float32)
    elif model_type == 'boid':
        # Neural boid: perception(13) -> hidden(64) -> velocity_delta(3)
        w1 = np.random.randn(13, 64).astype(np.float32) * 0.1
        b1 = np.zeros(64, dtype=np.float32)
        w2 = np.random.randn(64, 3).astype(np.float32) * 0.01
        b2 = np.zeros(3, dtype=np.float32)
    elif model_type == 'flow':
        # Neural flow: position(3) -> hidden(32) -> flow_vector(3)
        w1 = np.random.randn(3, 32).astype(np.float32) * 0.1
        b1 = np.zeros(32, dtype=np.float32)
        w2 = np.random.randn(32, 3).astype(np.float32) * 0.01
        b2 = np.zeros(3, dtype=np.float32)
    else:
        print(f"Unknown model type: {model_type}")
        sys.exit(1)

    export_two_layer_weights(w1, b1, w2, b2, output_path)
    print(f"(These are RANDOM test weights, not trained)")


def main():
    parser = argparse.ArgumentParser(description='Export trained model weights for Unity')
    parser.add_argument('model_type', choices=['nca', 'boid', 'flow'],
                       help='Type of model to export')
    parser.add_argument('--model', type=str, default=None,
                       help='Path to the trained model (TF SavedModel, PyTorch .pt, or TF checkpoint)')
    parser.add_argument('--output', type=str, required=True,
                       help='Output path for the .bytes file')
    parser.add_argument('--format', choices=['tf', 'pytorch', 'checkpoint', 'random'],
                       default='random',
                       help='Model format (default: random for testing)')

    args = parser.parse_args()

    if args.format == 'random':
        generate_random_weights(args.output, args.model_type)
    elif args.format == 'tf':
        export_from_tensorflow(args.model, args.output, args.model_type)
    elif args.format == 'pytorch':
        export_from_pytorch(args.model, args.output, args.model_type)
    elif args.format == 'checkpoint':
        export_from_colab_checkpoint(args.model, args.output)


if __name__ == '__main__':
    main()

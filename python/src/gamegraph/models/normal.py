import math
from typing import cast

import torch


def cdf(z: torch.Tensor) -> torch.Tensor:
    return torch.exp(log_cdf(z))


def log_cdf(z: torch.Tensor) -> torch.Tensor:
    return cast(torch.Tensor, torch.special.log_ndtr(z))


def log_density(z: torch.Tensor) -> torch.Tensor:
    return -0.5 * z**2 - 0.5 * math.log(2 * math.pi)


def inverse_cdf(p: torch.Tensor) -> torch.Tensor:
    return cast(torch.Tensor, torch.special.ndtri(p))

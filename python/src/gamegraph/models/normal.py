from typing import cast

import torch


def cdf(z: torch.Tensor) -> torch.Tensor:
    return torch.exp(log_cdf(z))


def log_cdf(z: torch.Tensor) -> torch.Tensor:
    return cast(torch.Tensor, torch.special.log_ndtr(z))


def inverse_cdf(p: torch.Tensor) -> torch.Tensor:
    return cast(torch.Tensor, torch.special.ndtri(p))

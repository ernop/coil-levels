"""
LLM Provider Implementations

Concrete implementations for various LLM APIs.
These can be used with real API keys for actual idea generation.
"""

import os
import re
import time
from typing import Optional
from abc import ABC, abstractmethod
try:
    from .core import LLMInterface
except ImportError:
    from core import LLMInterface


class RateLimiter:
    """Simple rate limiter for API calls."""
    
    def __init__(self, calls_per_minute: int = 20):
        self.calls_per_minute = calls_per_minute
        self.call_times: list[float] = []
    
    def wait_if_needed(self):
        """Block if we've exceeded rate limit."""
        now = time.time()
        # Remove calls older than 1 minute
        self.call_times = [t for t in self.call_times if now - t < 60]
        
        if len(self.call_times) >= self.calls_per_minute:
            sleep_time = 60 - (now - self.call_times[0]) + 0.1
            if sleep_time > 0:
                time.sleep(sleep_time)
        
        self.call_times.append(time.time())


class AnthropicLLM(LLMInterface):
    """Anthropic Claude API implementation."""
    
    def __init__(self, 
                 model: str = "claude-sonnet-4-20250514",
                 api_key: Optional[str] = None):
        self._model = model
        self._api_key = api_key or os.environ.get("ANTHROPIC_API_KEY")
        self._rate_limiter = RateLimiter(calls_per_minute=30)
        self._client = None
    
    def _get_client(self):
        if self._client is None:
            try:
                import anthropic
                self._client = anthropic.Anthropic(api_key=self._api_key)
            except ImportError:
                raise ImportError("Please install anthropic: pip install anthropic")
        return self._client
    
    @property
    def name(self) -> str:
        return f"anthropic/{self._model}"
    
    def generate(self, prompt: str, temperature: float = 0.7) -> str:
        self._rate_limiter.wait_if_needed()
        client = self._get_client()
        
        message = client.messages.create(
            model=self._model,
            max_tokens=4096,
            temperature=temperature,
            messages=[{"role": "user", "content": prompt}]
        )
        
        return message.content[0].text
    
    def critique(self, idea: str, problem_context: str) -> tuple[float, str]:
        prompt = f"""
Problem Context:
{problem_context}

Proposed Idea:
{idea}

Please critically evaluate this idea:
1. Is the core insight valid?
2. Are there obvious flaws?
3. How likely to produce improvement?

Rate 0.0 to 1.0 and explain.
Format:
SCORE: [number]
REASONING: [analysis]
"""
        self._rate_limiter.wait_if_needed()
        response = self.generate(prompt, temperature=0.3)
        
        # Parse response
        score_match = re.search(r'SCORE:\s*([\d.]+)', response)
        score = float(score_match.group(1)) if score_match else 0.5
        
        reasoning_match = re.search(r'REASONING:\s*(.+)', response, re.DOTALL)
        reasoning = reasoning_match.group(1).strip() if reasoning_match else response
        
        return min(1.0, max(0.0, score)), reasoning


class OpenAILLM(LLMInterface):
    """OpenAI GPT API implementation."""
    
    def __init__(self,
                 model: str = "gpt-4-turbo-preview",
                 api_key: Optional[str] = None):
        self._model = model
        self._api_key = api_key or os.environ.get("OPENAI_API_KEY")
        self._rate_limiter = RateLimiter(calls_per_minute=30)
        self._client = None
    
    def _get_client(self):
        if self._client is None:
            try:
                from openai import OpenAI
                self._client = OpenAI(api_key=self._api_key)
            except ImportError:
                raise ImportError("Please install openai: pip install openai")
        return self._client
    
    @property
    def name(self) -> str:
        return f"openai/{self._model}"
    
    def generate(self, prompt: str, temperature: float = 0.7) -> str:
        self._rate_limiter.wait_if_needed()
        client = self._get_client()
        
        response = client.chat.completions.create(
            model=self._model,
            messages=[{"role": "user", "content": prompt}],
            temperature=temperature,
            max_tokens=4096
        )
        
        return response.choices[0].message.content
    
    def critique(self, idea: str, problem_context: str) -> tuple[float, str]:
        prompt = f"""
Problem Context:
{problem_context}

Proposed Idea:
{idea}

Evaluate this idea critically. Rate 0.0-1.0.
Format:
SCORE: [number]
REASONING: [analysis]
"""
        response = self.generate(prompt, temperature=0.3)
        
        score_match = re.search(r'SCORE:\s*([\d.]+)', response)
        score = float(score_match.group(1)) if score_match else 0.5
        
        reasoning_match = re.search(r'REASONING:\s*(.+)', response, re.DOTALL)
        reasoning = reasoning_match.group(1).strip() if reasoning_match else response
        
        return min(1.0, max(0.0, score)), reasoning


class GoogleLLM(LLMInterface):
    """Google Gemini API implementation."""
    
    def __init__(self,
                 model: str = "gemini-pro",
                 api_key: Optional[str] = None):
        self._model = model
        self._api_key = api_key or os.environ.get("GOOGLE_API_KEY")
        self._rate_limiter = RateLimiter(calls_per_minute=30)
        self._genai = None
    
    def _get_model(self):
        if self._genai is None:
            try:
                import google.generativeai as genai
                genai.configure(api_key=self._api_key)
                self._genai = genai.GenerativeModel(self._model)
            except ImportError:
                raise ImportError("Please install google-generativeai: pip install google-generativeai")
        return self._genai
    
    @property
    def name(self) -> str:
        return f"google/{self._model}"
    
    def generate(self, prompt: str, temperature: float = 0.7) -> str:
        self._rate_limiter.wait_if_needed()
        model = self._get_model()
        
        response = model.generate_content(
            prompt,
            generation_config={
                'temperature': temperature,
                'max_output_tokens': 4096,
            }
        )
        
        return response.text
    
    def critique(self, idea: str, problem_context: str) -> tuple[float, str]:
        prompt = f"""
Problem: {problem_context}

Idea: {idea}

Rate this idea 0.0-1.0. Format:
SCORE: [number]
REASONING: [why]
"""
        response = self.generate(prompt, temperature=0.3)
        
        score_match = re.search(r'SCORE:\s*([\d.]+)', response)
        score = float(score_match.group(1)) if score_match else 0.5
        
        reasoning_match = re.search(r'REASONING:\s*(.+)', response, re.DOTALL)
        reasoning = reasoning_match.group(1).strip() if reasoning_match else response
        
        return min(1.0, max(0.0, score)), reasoning


class EnsembleLLM(LLMInterface):
    """Combines multiple LLMs for diverse idea generation."""
    
    def __init__(self, llms: list[LLMInterface]):
        self._llms = llms
    
    @property
    def name(self) -> str:
        return "ensemble/" + "+".join(llm.name.split("/")[-1] for llm in self._llms)
    
    def generate(self, prompt: str, temperature: float = 0.7) -> str:
        """Generate from all LLMs and combine."""
        all_responses = []
        
        for llm in self._llms:
            try:
                response = llm.generate(prompt, temperature)
                all_responses.append(f"## Ideas from {llm.name}\n\n{response}")
            except Exception as e:
                all_responses.append(f"## {llm.name}: Error - {e}")
        
        return "\n\n---\n\n".join(all_responses)
    
    def critique(self, idea: str, problem_context: str) -> tuple[float, str]:
        """Average critique scores across LLMs."""
        scores = []
        reasonings = []
        
        for llm in self._llms:
            try:
                score, reasoning = llm.critique(idea, problem_context)
                scores.append(score)
                reasonings.append(f"{llm.name}: {reasoning[:200]}")
            except Exception:
                pass
        
        if not scores:
            return 0.5, "No valid critiques"
        
        avg_score = sum(scores) / len(scores)
        combined_reasoning = "\n".join(reasonings)
        
        return avg_score, combined_reasoning


def create_default_ensemble(api_keys: Optional[dict] = None) -> list[LLMInterface]:
    """
    Create a default ensemble of LLMs.
    
    Args:
        api_keys: Dict with 'anthropic', 'openai', 'google' keys
    
    Returns:
        List of available LLM interfaces
    """
    api_keys = api_keys or {}
    llms = []
    
    # Try to create each LLM
    try:
        llms.append(AnthropicLLM(
            model="claude-sonnet-4-20250514",
            api_key=api_keys.get('anthropic')
        ))
    except Exception:
        pass
    
    try:
        llms.append(OpenAILLM(
            model="gpt-4-turbo-preview",
            api_key=api_keys.get('openai')
        ))
    except Exception:
        pass
    
    try:
        llms.append(GoogleLLM(
            model="gemini-pro",
            api_key=api_keys.get('google')
        ))
    except Exception:
        pass
    
    return llms
